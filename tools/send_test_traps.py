import asyncio
import json
import sys
import uuid

import websockets


async def receive_packets(socket):
    payload = await socket.recv()
    if isinstance(payload, bytes):
        raise RuntimeError("Compressed packets are not supported by this test helper")
    return json.loads(payload)


async def main(item_names):
    async with websockets.connect(
        "ws://localhost:38281",
        compression=None,
    ) as socket:
        await receive_packets(socket)
        await socket.send(json.dumps([{
            "cmd": "Connect",
            "password": None,
            "game": "Oh So Hero!",
            "name": "Player",
            "uuid": uuid.uuid4().hex,
            "version": {
                "major": 0,
                "minor": 7,
                "build": 0,
                "class": "Version",
            },
            "items_handling": 7,
            "tags": [],
            "slot_data": False,
        }]))

        packets = await receive_packets(socket)
        if not any(packet.get("cmd") == "Connected" for packet in packets):
            raise RuntimeError(f"Login failed: {packets}")

        for item_name in item_names:
            await socket.send(json.dumps([{
                "cmd": "Say",
                "text": f"!getitem {item_name}",
            }]))
            await asyncio.sleep(0.25)

        await asyncio.sleep(0.5)


if __name__ == "__main__":
    asyncio.run(main(sys.argv[1:]))
