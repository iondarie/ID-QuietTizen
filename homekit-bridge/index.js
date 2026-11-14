'use strict';

/*
 * homekit-bridge — a minimal HTTP front-end over hap-node-client.
 *
 * Discovers Homebridge via mDNS, lists its accessories, and lets a simple
 * HTTP client (e.g. Quiet Home on the TV) read/write HAP characteristics.
 *
 * Environment:
 *   HOMEBRIDGE_PIN    (required) Homebridge setup PIN, e.g. 031-45-154
 *   PORT              (default 3000)
 *   REFRESH_SECONDS   (default 60) mDNS re-discovery interval
 *   HTTP_TOKEN        (optional) if set, every request needs "Authorization: Bearer <token>"
 *   DEBUG             (optional) "1" to enable verbose logging
 */

const http = require('http');
const { HAPNodeJSClient } = require('hap-node-client');

const PIN = (process.env.HOMEBRIDGE_PIN || '').trim();
const PORT = parseInt(process.env.PORT || '3000', 10);
const REFRESH = parseInt(process.env.REFRESH_SECONDS || '60', 10);
const TOKEN = (process.env.HTTP_TOKEN || '').trim();

if (!PIN) {
  console.error('FATAL: HOMEBRIDGE_PIN environment variable is required');
  process.exit(1);
}

// HAP type IDs: short form (as returned by the insecure /accessories endpoint)
// with full-UUID fallback.
const UUID = (hex) => `000000${hex}-0000-1000-8000-0026BB765291`;
const T = {
  AccessoryInformation: '3E',
  Name: '23',
  On: '25',
  Brightness: '08',
  Lightbulb: '43',
  Switch: '49',
  Outlet: '47',
  Fan: '40',
  LockMechanism: '45',
  TargetLockState: '1E',
  CurrentLockState: '1D',
  GarageDoorOpener: '41',
  TargetDoorState: '32',
  CurrentDoorState: '0E',
  Thermostat: '4A',
  TargetTemperature: '35',
  CurrentTemperature: '11',
};

function isType(actual, key) {
  return actual === T[key] || actual === UUID(T[key]);
}

function findService(acc, key) {
  return (acc.services || []).find((s) => isType(s.type, key));
}

function findChar(service, key) {
  if (!service) return null;
  return (service.characteristics || []).find((c) => isType(c.type, key)) || null;
}

function charValue(service, key) {
  const c = findChar(service, key);
  return c === null ? undefined : c.value;
}

function nameOf(acc) {
  const info = findService(acc, 'AccessoryInformation');
  const name = charValue(info, 'Name');
  return name !== undefined && String(name).trim() !== '' ? String(name) : `Accessory ${acc.aid}`;
}

function describePrimary(acc) {
  const svcMap = [
    ['light', 'Lightbulb'],
    ['switch', 'Switch'],
    ['outlet', 'Outlet'],
    ['fan', 'Fan'],
    ['lock', 'LockMechanism'],
    ['door', 'GarageDoorOpener'],
    ['thermostat', 'Thermostat'],
  ];
  for (const [kind, svcKey] of svcMap) {
    const svc = findService(acc, svcKey);
    if (!svc) continue;
    const on = charValue(svc, 'On');
    if (on !== undefined) {
      return {
        kind: kind === 'light' ? 'light' : 'switch',
        iid: findChar(svc, 'On').iid,
        value: on,
        brightness: charValue(svc, 'Brightness'),
        brightnessIid: findChar(svc, 'Brightness') ? findChar(svc, 'Brightness').iid : null,
      };
    }
    const lock = charValue(svc, 'TargetLockState');
    if (lock !== undefined) {
      return {
        kind: 'lock',
        iid: findChar(svc, 'TargetLockState').iid,
        value: lock,
        current: charValue(svc, 'CurrentLockState'),
      };
    }
    const door = charValue(svc, 'TargetDoorState');
    if (door !== undefined) {
      return {
        kind: 'door',
        iid: findChar(svc, 'TargetDoorState').iid,
        value: door,
        current: charValue(svc, 'CurrentDoorState'),
      };
    }
    const temp = charValue(svc, 'TargetTemperature');
    if (temp !== undefined) {
      return {
        kind: 'thermostat',
        iid: findChar(svc, 'TargetTemperature').iid,
        value: temp,
        current: charValue(svc, 'CurrentTemperature'),
      };
    }
  }
  return null;
}

const client = new HAPNodeJSClient({
  debug: process.env.DEBUG === '1',
  refresh: REFRESH,
  timeout: 15,
  reqTimeout: 7000,
  pin: PIN,
  filter: false,
});

let ready = false;
let readyError = null;

client.on('Ready', () => {
  ready = true;
  readyError = null;
  console.log('hap-node-client ready (Homebridge discovered and reachable)');
});

client.on('error', (err) => {
  readyError = err && err.message ? err.message : String(err);
  console.error('hap-node-client error:', readyError);
});

function flattenAccessories() {
  const out = [];
  client.HAPaccessories((endpoints) => {
    (endpoints || []).forEach((entry) => {
      const deviceID = entry.deviceID || (entry.txt && entry.txt.id) || 'unknown';
      const accs = (entry.accessories && entry.accessories.accessories) || [];
      accs.forEach((acc) => {
        if (!acc || acc.aid === undefined) return;
        out.push({
          deviceID,
          aid: acc.aid,
          name: nameOf(acc),
          primary: describePrimary(acc),
        });
      });
    });
  });
  out.sort((a, b) => a.name.localeCompare(b.name));
  return out;
}

function controlByDeviceID(deviceID, aid, iid, value) {
  return new Promise((resolve, reject) => {
    const body = JSON.stringify({ characteristics: [{ aid, iid, value }] });
    client.HAPcontrolByDeviceID(deviceID, body, (err, response) => {
      if (err) return reject(err);
      resolve(response);
    });
  });
}

function sendJson(res, status, obj) {
  const body = JSON.stringify(obj);
  res.writeHead(status, { 'Content-Type': 'application/json' });
  res.end(body);
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let data = '';
    req.on('data', (c) => {
      data += c;
      if (data.length > 1_000_000) {
        req.destroy();
        reject(new Error('request body too large'));
      }
    });
    req.on('end', () => resolve(data));
    req.on('error', reject);
  });
}

const server = http.createServer(async (req, res) => {
  try {
    if (TOKEN && req.headers.authorization !== `Bearer ${TOKEN}`) {
      return sendJson(res, 401, { error: 'unauthorized' });
    }

    const url = new URL(req.url, `http://${req.headers.host || 'localhost'}`);

    if (req.method === 'GET' && url.pathname === '/health') {
      return sendJson(res, 200, { ok: ready, error: readyError });
    }

    if (req.method === 'GET' && url.pathname === '/accessories') {
      return sendJson(res, 200, { ok: ready, error: readyError, accessories: flattenAccessories() });
    }

    if (req.method === 'POST' && url.pathname === '/control') {
      const raw = await readBody(req);
      let payload;
      try {
        payload = JSON.parse(raw || '{}');
      } catch {
        return sendJson(res, 400, { error: 'invalid JSON body' });
      }
      const { deviceID, aid, iid, value } = payload;
      if (!deviceID || aid === undefined || iid === undefined || value === undefined) {
        return sendJson(res, 400, { error: 'body must include deviceID, aid, iid, value' });
      }
      try {
        await controlByDeviceID(deviceID, aid, iid, value);
        return sendJson(res, 200, { ok: true });
      } catch (e) {
        return sendJson(res, 502, { error: e && e.message ? e.message : String(e) });
      }
    }

    return sendJson(res, 404, { error: 'not found' });
  } catch (e) {
    return sendJson(res, 500, { error: e && e.message ? e.message : String(e) });
  }
});

server.listen(PORT, () => {
  console.log(`homekit-bridge listening on port ${PORT}`);
});
