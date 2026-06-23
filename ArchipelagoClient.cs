using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Models;
using Newtonsoft.Json;

namespace OhSoHeroArchipelago;

internal sealed class ArchipelagoClient : IDisposable
{
    private const string GameName = "Oh So Hero!";
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(10);
    private static readonly string RecoveryLocationsPath = Path.Combine(
        BepInEx.Paths.ConfigPath,
        "OhSoHeroArchipelago",
        "recovery_locations.txt");

    private readonly string _server;
    private readonly string _slot;
    private readonly string _password;
    private readonly string _uuid;
    private readonly bool _autoConnect;
    private readonly ConcurrentQueue<QueuedItem> _receivedItems =
        new ConcurrentQueue<QueuedItem>();
    private readonly ConcurrentQueue<string> _messages =
        new ConcurrentQueue<string>();
    private readonly ConcurrentQueue<DeathLink> _receivedDeathLinks =
        new ConcurrentQueue<DeathLink>();
    private readonly ConcurrentDictionary<string, byte> _pendingLocations =
        new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
    private readonly ClientStateStore _stateStore = new ClientStateStore();

    private ArchipelagoSession _session;
    private DeathLinkService _deathLinkService;
    private SlotState _slotState;
    private Task _connectionTask;
    private DateTime _nextReconnectUtc;
    private int _dequeuedItemCount;
    private string _goal = "defeat_bates";
    private int _sceneGoalRequiredCount = 158;
    private bool _disposed;
    private int _deathLinkSendInProgress;

    internal bool Connected => _session?.Socket?.Connected == true;
    internal bool HasActiveSlotState => _slotState != null;
    internal bool IsCollectAllScenesGoal =>
        string.Equals(_goal, "collect_all_scenes", StringComparison.Ordinal);

    internal bool HasReceivedItem(string itemName)
    {
        return _slotState?.ReceivedItems.Contains(itemName) ?? false;
    }

    internal bool IsCollectAllScenesGoalIncomplete()
    {
        return IsCollectAllScenesGoal && !IsSceneGoalAchieved();
    }

    internal bool IsZoneUnlocked(string zoneName)
    {
        return string.Equals(zoneName, "SheoIslandsBeach", StringComparison.Ordinal) ||
            (_slotState?.UnlockedZones.Contains(zoneName) ?? false);
    }

    internal string LastUnlockedZone => _slotState?.LastUnlockedZone;

    internal void RememberUnlockedZone(string zoneName)
    {
        if (_slotState == null ||
            string.Equals(_slotState.LastUnlockedZone, zoneName, StringComparison.Ordinal))
        {
            return;
        }

        _slotState.LastUnlockedZone = zoneName;
        _stateStore.Save();
    }

    internal ArchipelagoClient(
        string server,
        string slot,
        string password,
        string uuid,
        bool autoConnect)
    {
        _server = server?.Trim() ?? string.Empty;
        _slot = slot?.Trim() ?? string.Empty;
        _password = password ?? string.Empty;
        _uuid = uuid;
        _autoConnect = autoConnect;
        _nextReconnectUtc = DateTime.UtcNow;
        LoadRecoveryLocations();
    }

    internal void Update()
    {
        while (_messages.TryDequeue(out string message))
        {
            Plugin.Log.LogInfo(message);
        }

        if (_autoConnect && !_disposed && !Connected &&
            !string.IsNullOrWhiteSpace(_slot) &&
            DateTime.UtcNow >= _nextReconnectUtc &&
            (_connectionTask == null || _connectionTask.IsCompleted))
        {
            _nextReconnectUtc = DateTime.UtcNow + ReconnectDelay;
            _connectionTask = Task.Run(Connect);
        }

        ProcessReceivedItems();
        ProcessReceivedDeathLinks();
        SendPendingLocations();
        EvaluateGoal();
    }

    private void Connect()
    {
        try
        {
            Uri serverUri = CreateServerUri(_server);
            var session = ArchipelagoSessionFactory.CreateSession(serverUri);
            _dequeuedItemCount = 0;
            session.Items.ItemReceived += OnItemReceived;
            session.MessageLog.OnMessageReceived += OnMessageReceived;

            LoginResult result = session.TryConnectAndLogin(
                GameName,
                _slot,
                ItemsHandlingFlags.AllItems,
                new Version(0, 6, 7),
                Array.Empty<string>(),
                _uuid,
                string.IsNullOrEmpty(_password) ? null : _password,
                true);

            if (!result.Successful)
            {
                var failure = (LoginFailure)result;
                _messages.Enqueue(
                    $"Archipelago login failed: {string.Join(", ", failure.Errors)}");
                session.Items.ItemReceived -= OnItemReceived;
                session.MessageLog.OnMessageReceived -= OnMessageReceived;
                session.Socket.DisconnectAsync().GetAwaiter().GetResult();
                return;
            }

            string stateKey = $"{serverUri}|{session.RoomState.Seed}|{_slot}";
            _slotState = _stateStore.GetOrCreate(stateKey);
            _session = session;

            var successful = (LoginSuccessful)result;
            _goal = GetSlotDataString(
                successful.SlotData, "goal", "defeat_bates");
            _sceneGoalRequiredCount = GetSlotDataInt(
                successful.SlotData, "scene_goal_required_count", 158);
            if (GetSlotDataBool(successful.SlotData, "death_link"))
            {
                _deathLinkService = DeathLinkProvider.CreateDeathLinkService(session);
                _deathLinkService.OnDeathLinkReceived += OnDeathLinkReceived;
                _deathLinkService.EnableDeathLink();
                _messages.Enqueue("DeathLink enabled for this slot.");
            }

            _messages.Enqueue(
                $"Connected to Archipelago: {_slot} | Seed: {session.RoomState.Seed}");
        }
        catch (Exception exception)
        {
            _messages.Enqueue($"Archipelago connection failed: {exception.Message}");
        }
    }

    private static bool GetSlotDataBool(
        IReadOnlyDictionary<string, object> slotData,
        string key)
    {
        if (slotData == null || !slotData.TryGetValue(key, out object value))
        {
            return false;
        }

        if (value is bool boolValue)
        {
            return boolValue;
        }

        return Convert.ToInt32(value) != 0;
    }

    private static string GetSlotDataString(
        IReadOnlyDictionary<string, object> slotData,
        string key,
        string defaultValue)
    {
        if (slotData == null || !slotData.TryGetValue(key, out object value))
        {
            return defaultValue;
        }

        return Convert.ToString(value) ?? defaultValue;
    }

    private static int GetSlotDataInt(
        IReadOnlyDictionary<string, object> slotData,
        string key,
        int defaultValue)
    {
        if (slotData == null || !slotData.TryGetValue(key, out object value))
        {
            return defaultValue;
        }

        return Convert.ToInt32(value);
    }

    private void OnDeathLinkReceived(DeathLink deathLink)
    {
        if (deathLink == null ||
            string.Equals(deathLink.Source, _slot, StringComparison.Ordinal))
        {
            return;
        }

        _receivedDeathLinks.Enqueue(deathLink);
    }

    private void ProcessReceivedDeathLinks()
    {
        while (_receivedDeathLinks.TryPeek(out DeathLink deathLink))
        {
            if (!DeathLinkManager.TryApply(deathLink))
            {
                return;
            }

            _receivedDeathLinks.TryDequeue(out _);
        }
    }

    internal void SendDeathLink()
    {
        DeathLinkService service = _deathLinkService;
        if (service == null || !Connected ||
            Interlocked.Exchange(ref _deathLinkSendInProgress, 1) != 0)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                service.SendDeathLink(new DeathLink(
                    _slot,
                    $"{_slot} was knocked out in Oh So Hero!."));
                _messages.Enqueue("DeathLink sent.");
            }
            catch (Exception exception)
            {
                _messages.Enqueue(
                    $"Could not send DeathLink: {exception.Message}");
            }
            finally
            {
                Interlocked.Exchange(ref _deathLinkSendInProgress, 0);
            }
        });
    }

    private void EvaluateGoal()
    {
        ArchipelagoSession session = _session;
        if (session?.Socket?.Connected != true || _slotState == null ||
            _slotState.GoalReported)
        {
            return;
        }

        bool achieved;
        string details;
        if (string.Equals(_goal, "collect_all_scenes", StringComparison.Ordinal))
        {
            int sceneCount = CountSceneGoalLocations(session);
            achieved = sceneCount >= _sceneGoalRequiredCount;
            details = $"{sceneCount}/{_sceneGoalRequiredCount} non-Bates scenes";
        }
        else
        {
            string batesLocationName = LocationNameMapper.ToDisplay("Defeat_Bates");
            long batesLocation = session.Locations.GetLocationIdFromName(
                GameName, batesLocationName);
            achieved = batesLocation > 0 &&
                session.Locations.AllLocationsChecked.Contains(batesLocation);
            details = batesLocationName;
        }

        if (!achieved)
        {
            return;
        }

        try
        {
            session.SetGoalAchieved();
            _slotState.GoalReported = true;
            _stateStore.Save();
            Plugin.Log.LogWarning($"Archipelago goal achieved: {details}");
        }
        catch (Exception exception)
        {
            Plugin.Log.LogError(
                $"Could not report Archipelago goal: {exception.Message}");
        }
    }

    private bool IsSceneGoalAchieved()
    {
        ArchipelagoSession session = _session;
        return session?.Socket?.Connected == true &&
            CountSceneGoalLocations(session) >= _sceneGoalRequiredCount;
    }

    private int CountSceneGoalLocations(ArchipelagoSession session)
    {
        if (session == null)
        {
            return 0;
        }

        return session.Locations.AllLocationsChecked.Count(id =>
        {
            string name = session.Locations.GetLocationNameFromId(id, GameName);
            string internalName = LocationNameMapper.ToInternal(name);
            return IsSceneGoalLocation(internalName);
        });
    }

    private static bool IsSceneGoalLocation(string locationName)
    {
        return !string.IsNullOrEmpty(locationName) &&
            !locationName.StartsWith("Bates", StringComparison.Ordinal) &&
            !locationName.StartsWith("Defeat_", StringComparison.Ordinal) &&
            !locationName.StartsWith("Pickup_", StringComparison.Ordinal) &&
            !locationName.StartsWith("TurnIn_", StringComparison.Ordinal) &&
            !locationName.StartsWith("Visit_", StringComparison.Ordinal) &&
            !locationName.StartsWith("Buy_", StringComparison.Ordinal) &&
            !locationName.Contains("_Talked", StringComparison.Ordinal) &&
            !locationName.Contains("_EnemyGauntlet", StringComparison.Ordinal);
    }

    private void OnItemReceived(ReceivedItemsHelper helper)
    {
        while (helper.Any())
        {
            ItemInfo item = helper.DequeueItem();
            int sequence = Interlocked.Increment(ref _dequeuedItemCount) - 1;
            _receivedItems.Enqueue(new QueuedItem(sequence, item));
        }
    }

    private void OnMessageReceived(LogMessage message)
    {
        if (message != null)
        {
            _messages.Enqueue($"AP: {message}");
        }
    }

    private void ProcessReceivedItems()
    {
        if (_slotState == null)
        {
            return;
        }

        while (_receivedItems.TryPeek(out QueuedItem queued))
        {
            if (queued.Sequence < _slotState.ProcessedItemCount)
            {
                if (_slotState.ReceivedItems.Add(queued.Item.ItemName))
                {
                    _stateStore.Save();
                }
                _receivedItems.TryDequeue(out _);
                continue;
            }

            if (queued.Sequence > _slotState.ProcessedItemCount)
            {
                Plugin.Log.LogWarning(
                    $"Waiting for missing item index {_slotState.ProcessedItemCount}.");
                return;
            }

            if (!ItemApplier.TryApply(queued.Item, _slotState))
            {
                return;
            }

            _receivedItems.TryDequeue(out _);
            _slotState.ProcessedItemCount++;
            _stateStore.Save();
            Plugin.Log.LogInfo(
                $"Applied AP item #{queued.Sequence}: {queued.Item.ItemName}");
        }
    }

    internal void CompleteLocation(string locationName)
    {
        if (string.IsNullOrWhiteSpace(locationName))
        {
            return;
        }

        string displayLocationName = LocationNameMapper.ToDisplay(locationName);
        _pendingLocations.TryAdd(displayLocationName, 0);
        SendPendingLocations();
    }

    private void SendPendingLocations()
    {
        ArchipelagoSession session = _session;
        if (session?.Socket?.Connected != true)
        {
            return;
        }

        foreach (string locationName in _pendingLocations.Keys)
        {
            try
            {
                long id = session.Locations.GetLocationIdFromName(GameName, locationName);
                if (id <= 0)
                {
                    Plugin.Log.LogWarning($"Unknown Archipelago location: {locationName}");
                    _pendingLocations.TryRemove(locationName, out _);
                    continue;
                }

                if (!session.Locations.AllLocationsChecked.Contains(id))
                {
                    session.Locations.CompleteLocationChecks(id);
                    Plugin.Log.LogInfo($"Location sent: {locationName} ({id})");
                }

                _pendingLocations.TryRemove(locationName, out _);
                DeleteEmptyRecoveryFile();
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError(
                    $"Could not send location {locationName}: {exception.Message}");
            }
        }
    }

    private void LoadRecoveryLocations()
    {
        try
        {
            if (!File.Exists(RecoveryLocationsPath))
            {
                return;
            }

            foreach (string line in File.ReadAllLines(RecoveryLocationsPath))
            {
                string locationName = line.Trim();
                if (!string.IsNullOrEmpty(locationName))
                {
                    _pendingLocations.TryAdd(
                        LocationNameMapper.ToDisplay(locationName),
                        0);
                }
            }
        }
        catch (Exception exception)
        {
            Plugin.Log.LogWarning(
                $"Could not load recovery locations: {exception.Message}");
        }
    }

    private void DeleteEmptyRecoveryFile()
    {
        if (!_pendingLocations.IsEmpty || !File.Exists(RecoveryLocationsPath))
        {
            return;
        }

        try
        {
            File.Delete(RecoveryLocationsPath);
        }
        catch (Exception exception)
        {
            Plugin.Log.LogWarning(
                $"Could not delete recovery locations: {exception.Message}");
        }
    }

    private static Uri CreateServerUri(string server)
    {
        string value = string.IsNullOrWhiteSpace(server)
            ? "localhost:38281"
            : server;
        if (!value.Contains("://"))
        {
            value = value.StartsWith("archipelago.gg", StringComparison.OrdinalIgnoreCase)
                ? $"wss://{value}"
                : $"ws://{value}";
        }

        return new Uri(value);
    }

    public void Dispose()
    {
        _disposed = true;
        ArchipelagoSession session = _session;
        _session = null;
        if (session == null)
        {
            return;
        }

        try
        {
            if (_deathLinkService != null)
            {
                _deathLinkService.OnDeathLinkReceived -= OnDeathLinkReceived;
                _deathLinkService = null;
            }

            session.Items.ItemReceived -= OnItemReceived;
            session.MessageLog.OnMessageReceived -= OnMessageReceived;
            _ = session.Socket.DisconnectAsync();
        }
        catch (Exception exception)
        {
            Plugin.Log.LogWarning(
                $"Archipelago disconnect failed: {exception.Message}");
        }
    }

    private readonly struct QueuedItem
    {
        internal int Sequence { get; }
        internal ItemInfo Item { get; }

        internal QueuedItem(int sequence, ItemInfo item)
        {
            Sequence = sequence;
            Item = item;
        }
    }
}

internal sealed class ClientStateStore
{
    private readonly string _path = Path.Combine(
        BepInEx.Paths.ConfigPath,
        "OhSoHeroArchipelago",
        "client_state.json");
    private ClientStateFile _data;

    internal ClientStateStore()
    {
        Load();
    }

    internal SlotState GetOrCreate(string key)
    {
        if (!_data.Slots.TryGetValue(key, out SlotState state))
        {
            state = new SlotState();
            _data.Slots[key] = state;
            Save();
        }

        state.UnlockedZones ??= new HashSet<string>(StringComparer.Ordinal);
        state.ReceivedItems ??= new HashSet<string>(StringComparer.Ordinal);

        return state;
    }

    internal void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path));
        File.WriteAllText(
            _path,
            JsonConvert.SerializeObject(_data, Formatting.Indented));
    }

    private void Load()
    {
        try
        {
            _data = File.Exists(_path)
                ? JsonConvert.DeserializeObject<ClientStateFile>(
                    File.ReadAllText(_path))
                : new ClientStateFile();
        }
        catch (Exception exception)
        {
            Plugin.Log?.LogWarning(
                $"Could not load client state: {exception.Message}");
            _data = new ClientStateFile();
        }

        if (_data?.Slots == null)
        {
            _data = new ClientStateFile();
        }
    }
}

internal sealed class ClientStateFile
{
    public Dictionary<string, SlotState> Slots { get; set; } =
        new Dictionary<string, SlotState>(StringComparer.Ordinal);
}

internal sealed class SlotState
{
    public int ProcessedItemCount { get; set; }
    public HashSet<string> UnlockedZones { get; set; } =
        new HashSet<string>(StringComparer.Ordinal);
    public HashSet<string> ReceivedItems { get; set; } =
        new HashSet<string>(StringComparer.Ordinal);
    public string LastUnlockedZone { get; set; }
    public bool GoalReported { get; set; }
}
