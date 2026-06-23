param(
    [Parameter(Mandatory = $true)]
    [string[]]$ItemNames
)

function Send-Packet {
    param($Socket, $Packet)
    $json = ConvertTo-Json -InputObject @($Packet) -Compress -Depth 8
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    $segment = New-Object ArraySegment[byte] -ArgumentList @(,$bytes)
    $Socket.SendAsync(
        $segment,
        [Net.WebSockets.WebSocketMessageType]::Text,
        $true,
        [Threading.CancellationToken]::None
    ).GetAwaiter().GetResult()
}

function Receive-Packet {
    param($Socket)
    $buffer = New-Object byte[] 65536
    $segment = New-Object ArraySegment[byte] -ArgumentList @(,$buffer)
    $stream = New-Object IO.MemoryStream
    do {
        $result = $Socket.ReceiveAsync(
            $segment,
            [Threading.CancellationToken]::None
        ).GetAwaiter().GetResult()
        $stream.Write($buffer, 0, $result.Count)
    } while (-not $result.EndOfMessage)
    return [Text.Encoding]::UTF8.GetString($stream.ToArray())
}

$socket = New-Object Net.WebSockets.ClientWebSocket
$socket.ConnectAsync(
    [Uri]'ws://localhost:38281',
    [Threading.CancellationToken]::None
).GetAwaiter().GetResult()

[void](Receive-Packet $socket)
$connect = [ordered]@{
    cmd = 'Connect'
    password = $null
    game = 'Oh So Hero!'
    name = 'Player'
    uuid = [Guid]::NewGuid().ToString('N')
    version = [ordered]@{
        major = 0
        minor = 7
        build = 0
        class = 'Version'
    }
    items_handling = 7
    tags = @()
    slot_data = $false
}
Send-Packet $socket $connect
$login = Receive-Packet $socket
if ($login -notmatch '"cmd"\s*:\s*"Connected"') {
    throw "Login failed: $login"
}

foreach ($itemName in $ItemNames) {
    Send-Packet $socket ([ordered]@{
        cmd = 'Say'
        text = "!getitem $itemName"
    })
    Start-Sleep -Milliseconds 300
}

Start-Sleep -Milliseconds 500
$socket.Abort()
$socket.Dispose()
