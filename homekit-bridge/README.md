# homekit-bridge

Un mic micro-serviciu Node.js care expune accesoriile **Homebridge** printr-un
API HTTP simplu, pentru ca launcher-ul Quiet Home (sau orice alt client) să le
controleze fără să implementeze HAP.

## Configurare (environment)

| Variabilă | Obligatorie | Descriere |
|---|---|---|
| `HOMEBRIDGE_PIN` | da | PIN-ul de pairing Homebridge (ex. `031-45-154`) |
| `PORT` | nu | port HTTP (default `3000`) |
| `REFRESH_SECONDS` | nu | interval re-descoperire mDNS (default `60`) |
| `HTTP_TOKEN` | nu | dacă e setat, fiecare request cere `Authorization: Bearer <token>` |
| `DEBUG` | nu | `1` pentru logare verbosă |

## Endpoint-uri

- `GET /health` → `{ "ok": true, "error": null }`
- `GET /accessories` → `{ "ok": true, "accessories": [ { deviceID, aid, name, primary } ] }`
- `POST /control` → body `{ "deviceID", "aid", "iid", "value" }` → setează o caracteristică

`primary` conține, dacă există, caracteristica principală de control
(`kind: light|switch|lock|door|thermostat`, `iid`, `value`, eventual `brightness`).

## Rulare

### Cu Docker (recomandat)

```sh
# editează HOMEBRIDGE_PIN în docker-compose.yml, apoi:
docker compose up -d --build
```

Pe **Linux** (unde rulează Homebridge), `network_mode: host` e necesar pentru
descoperirea mDNS + HAP. Pe **Docker Desktop** (macOS/Windows), înlocuiește
`network_mode: host` cu `ports: ["3000:3000"]`.

### Direct (fără Docker)

```sh
npm install
HOMEBRIDGE_PIN=031-45-154 PORT=3000 node index.js
```

## Note

- Descoperirea se face prin **mDNS (Bonjour)** pe portul 5353/UDP. Dacă serviciul
  rulează într-un container fără host networking, mDNS nu ajunge la Homebridge
  și `GET /accessories` va fi gol — folosește host networking pe Linux.
- PIN-ul e trimis ca header `authorization` către API-ul HAP al Homebridge-ului
  (modul „insecure" HAP), exact ca în `homebridge-alexa` / `Hap-NodeRED`.
- Fără `HTTP_TOKEN`, endpoint-urile sunt accesibile oricui din rețeaua locală.
