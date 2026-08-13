# Desktop Companion protocol 2

Base URL: `https://127.0.0.1:47831/v1`.

## Trust boundary

- The server listens only on the loopback interface over HTTPS.
- Every `/v1` request must carry the exact production add-in `Origin`.
- `GET /health` returns the per-process session token.
- `POST /commands` requires that token in `X-EDA-Token`.
- Commands are allow-listed and serialized; a second concurrent command receives HTTP 423.
- Clients older than Excel Data Assistant 3.1.0 receive HTTP 409.

## Endpoints

`GET /health` returns product, companion version, protocol version, capabilities and runtime state.

`POST /commands` accepts:

```json
{
  "command": "allow-listed-command",
  "source": "office-addin",
  "version": "3.2.0"
}
```

AI formula requests may additionally contain task text, range address and dimensions, explicit header metadata, headers, and the current formula. Cell values are not accepted or transmitted.
