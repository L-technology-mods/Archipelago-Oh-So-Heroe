import json
import os
import re
import subprocess
import urllib.parse
from pathlib import Path
from typing import Dict, Optional, Tuple

import Utils


CLIENT_STATE_NAME = "oh_so_hero_client.json"
PLUGIN_CONFIG_NAME = "lightsoul.ohsohero.archipelago.cfg"


def _load_state() -> Dict[str, str]:
    path = Path(Utils.user_path(CLIENT_STATE_NAME))
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}


def _save_state(state: Dict[str, str]) -> None:
    path = Path(Utils.user_path(CLIENT_STATE_NAME))
    path.write_text(json.dumps(state, indent=2), encoding="utf-8")


def _find_game_executable(saved_path: str) -> Optional[Path]:
    candidates = [
        Path(saved_path) if saved_path else None,
        Path(r"C:\Program Files (x86)\Steam\steamapps\common\Oh So Hero!\OhSoHero.exe"),
        Path(r"C:\Program Files\Steam\steamapps\common\Oh So Hero!\OhSoHero.exe"),
    ]
    for candidate in candidates:
        if candidate and candidate.is_file():
            return candidate
    return None


def _find_steam_executable() -> Optional[Path]:
    candidates = [
        Path(r"C:\Program Files (x86)\Steam\steam.exe"),
        Path(r"C:\Program Files\Steam\steam.exe"),
    ]
    for candidate in candidates:
        if candidate.is_file():
            return candidate
    return None


def _read_uri(args: Tuple[str, ...]) -> Tuple[str, str, str]:
    if not args:
        return "", "", ""

    parsed = urllib.parse.urlparse(args[0])
    if parsed.scheme != "archipelago" or not parsed.hostname:
        return "", "", ""

    server = parsed.hostname
    if parsed.port:
        server = f"{server}:{parsed.port}"
    slot = urllib.parse.unquote(parsed.username or "")
    password = urllib.parse.unquote(parsed.password or "")
    return server, slot, password


def _set_config_value(text: str, key: str, value: str) -> str:
    pattern = rf"(?m)^{re.escape(key)}\s*=.*$"
    replacement = f"{key} = {value}"
    if re.search(pattern, text):
        return re.sub(pattern, replacement, text)
    return text.rstrip() + f"\n{replacement}\n"


def _set_section_value(text: str, section: str, key: str, value: str) -> str:
    lines = text.splitlines()
    section_header = f"[{section}]"
    section_start = None
    section_end = len(lines)

    for index, line in enumerate(lines):
        stripped = line.strip()
        if stripped == section_header:
            section_start = index
            continue
        if section_start is not None and stripped.startswith("[") and stripped.endswith("]"):
            section_end = index
            break

    replacement = f"{key} = {value}"
    if section_start is None:
        if lines and lines[-1].strip():
            lines.append("")
        lines.extend([section_header, replacement])
        return "\n".join(lines) + "\n"

    pattern = re.compile(rf"^{re.escape(key)}\s*=.*$")
    for index in range(section_start + 1, section_end):
        if pattern.match(lines[index].strip()):
            lines[index] = replacement
            return "\n".join(lines) + "\n"

    lines.insert(section_end, replacement)
    return "\n".join(lines) + "\n"


def _enable_text_console(game_directory: Path) -> None:
    config_path = game_directory / "BepInEx" / "config" / "BepInEx.cfg"
    text = config_path.read_text(encoding="utf-8-sig") if config_path.is_file() else ""
    text = _set_section_value(text, "Logging.Console", "Enabled", "true")
    text = _set_section_value(text, "Logging.Console", "PreventClose", "true")
    config_path.parent.mkdir(parents=True, exist_ok=True)
    config_path.write_text(text, encoding="utf-8")


def _write_plugin_config(
    game_directory: Path,
    server: str,
    slot: str,
    password: str,
) -> None:
    config_directory = game_directory / "BepInEx" / "config"
    config_directory.mkdir(parents=True, exist_ok=True)
    config_path = config_directory / PLUGIN_CONFIG_NAME
    if config_path.is_file():
        text = config_path.read_text(encoding="utf-8-sig")
    else:
        text = "[Archipelago]\n"

    text = _set_config_value(text, "Server", server)
    text = _set_config_value(text, "Slot", slot)
    text = _set_config_value(text, "Password", password)
    text = _set_config_value(text, "AutoConnect", "true")
    config_path.write_text(text, encoding="utf-8")


def launch(*args: str) -> None:
    from tkinter import Tk, filedialog, messagebox, simpledialog

    root = Tk()
    root.withdraw()
    state = _load_state()

    executable = _find_game_executable(state.get("game_executable", ""))
    if executable is None:
        selected = filedialog.askopenfilename(
            title="Select OhSoHero.exe",
            filetypes=[("Oh So Hero executable", "OhSoHero.exe")],
        )
        if not selected:
            root.destroy()
            return
        executable = Path(selected)

    game_directory = executable.parent
    plugin_path = (
        game_directory
        / "BepInEx"
        / "plugins"
        / "OhSoHeroArchipelago"
        / "OhSoHeroArchipelago.dll"
    )
    if not plugin_path.is_file():
        messagebox.showerror(
            "Oh So Hero Client",
            "The BepInEx Archipelago plugin is not installed.\n"
            "Extract the Client folder from the release into the game folder.",
        )
        root.destroy()
        return

    server, slot, password = _read_uri(tuple(args))
    if not server:
        server = simpledialog.askstring(
            "Oh So Hero Client",
            "Server address:",
            initialvalue=state.get("server", "localhost:38281"),
        ) or ""
    if not slot:
        slot = simpledialog.askstring(
            "Oh So Hero Client",
            "Slot name:",
            initialvalue=state.get("slot", ""),
        ) or ""
    if password == "":
        password = simpledialog.askstring(
            "Oh So Hero Client",
            "Server password (optional):",
            show="*",
        ) or ""

    if not server or not slot:
        messagebox.showerror(
            "Oh So Hero Client",
            "A server address and slot name are required.",
        )
        root.destroy()
        return

    _write_plugin_config(game_directory, server, slot, password)
    _enable_text_console(game_directory)
    state.update({
        "game_executable": str(executable),
        "server": server,
        "slot": slot,
    })
    _save_state(state)
    game_environment = os.environ.copy()
    for proxy_name in (
        "HTTP_PROXY",
        "HTTPS_PROXY",
        "ALL_PROXY",
        "http_proxy",
        "https_proxy",
        "all_proxy",
    ):
        game_environment.pop(proxy_name, None)
    steam_executable = _find_steam_executable()
    if steam_executable is not None:
        subprocess.Popen(
            [str(steam_executable), "-applaunch", "2086050"],
            env=game_environment,
        )
    else:
        subprocess.Popen(
            [str(executable)],
            cwd=str(game_directory),
            env=game_environment,
        )
    root.destroy()


if __name__ == "__main__":
    import sys

    launch(*sys.argv[1:])
