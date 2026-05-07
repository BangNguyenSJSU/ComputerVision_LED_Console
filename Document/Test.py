"""Sample reader for the binary TCP status stream on port 9091.

Wire format (one frame per captured tick):
    byte 0      = N (LED count, 0..255)
    byte 1+i*2  = MarkerId (0..255)
    byte 2+i*2  = packed status byte: high nibble = LedColor, low nibble = LedStatus

LedStatus: 0 Unknown, 1 Off, 2 On
LedColor:  0 Unknown, 1 Red, 2 Yellow, 3 Green
"""

import socket

HOST, PORT = "127.0.0.1", 9091
STATUS = {0: "Unknown", 1: "Off", 2: "On"}
COLOR = {0: "Unknown", 1: "Red", 2: "Yellow", 3: "Green"}

with socket.create_connection((HOST, PORT)) as sock:
    buf = b""
    while True:
        chunk = sock.recv(4096)
        if not chunk:
            break
        buf += chunk
        while buf:
            n = buf[0]
            frame_len = 1 + n * 2
            if len(buf) < frame_len:
                break
            frame = buf[:frame_len]
            payload = frame[1:]
            buf = buf[frame_len:]
            print(f"raw: {frame.hex(' ')}")
            for i in range(n):
                mid, packed = payload[2 * i], payload[2 * i + 1]
                st = packed & 0x0F
                col = (packed >> 4) & 0x0F
                print(f"  LED #{mid}: {COLOR.get(col, '?')} {STATUS.get(st, '?')}")
