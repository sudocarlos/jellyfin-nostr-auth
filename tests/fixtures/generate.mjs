// Generates deterministic Nostr test fixtures for the plugin's behavior tests.
//
// All keys are FIXED throwaway test keys so output is reproducible. Timestamps are
// constants; each fixture carries the `now` a verifier should use, so staleness tests
// never expire. Run: npm install && node generate.mjs
import { writeFileSync } from 'node:fs';
import * as NostrTools from 'nostr-tools';
import { createHash } from 'node:crypto';

const { generateSecretKey, getPublicKey, finalizeEvent, nip19, nip44 } = NostrTools;

const hex = (bytes) => Buffer.from(bytes).toString('hex');
const fromHex = (h) => new Uint8Array(Buffer.from(h, 'hex'));

// Fixed test keys — DO NOT use anywhere real.
const KEYS = {
  list: {
    skHex: '0b9f3a8f1c2e4d5b6a7c8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8090',
  },
  userAuthorized: {
    skHex: '1111111111111111111111111111111111111111111111111111111111111111',
  },
  userUnauthorized: {
    skHex: '2222222222222222222222222222222222222222222222222222222222222222',
  },
  attacker: {
    skHex: '3333333333333333333333333333333333333333333333333333333333333333',
  },
};
for (const k of Object.values(KEYS)) k.pkHex = getPublicKey(fromHex(k.skHex));

const describeKey = (k) => ({
  name: k === KEYS.list ? 'list' : k === KEYS.userAuthorized ? 'userAuthorized' : k === KEYS.userUnauthorized ? 'userUnauthorized' : 'attacker',
  sk: nip19.nsecEncode(fromHex(k.skHex)),
  npub: nip19.npubEncode(k.pkHex),
  pubkeyHex: k.pkHex,
});

// ---- allowlist fixtures ------------------------------------------------------

// NIP-51 private list: content is a stringified JSON tags-array, NIP-44-encrypted
// to self (conversation key from the author's own keypair).
const allowedTags = JSON.stringify([
  ['p', KEYS.userAuthorized.pkHex],
]);

// NOTE: deterministic nonce (only for reproducibility of throwaway test keys —
// a real publisher must use a random nonce; ChaCha20 nonce reuse is unsafe).
const encryptToSelf = (plaintext, now) => {
  const convKey = nip44.v2.utils.getConversationKey(fromHex(KEYS.list.skHex), KEYS.list.pkHex);
  return nip44.v2.encrypt(plaintext, convKey, fromHex(hex(new Uint8Array(32).fill(now & 0xff))));
};

const BASE = 1760000000; // fixture epoch; verifiers use the `now` field, never real time

const encryptedListEvent = finalizeEvent(
  {
    kind: 10000,
    created_at: BASE,
    tags: [],
    content: encryptToSelf(allowedTags, BASE),
  },
  fromHex(KEYS.list.skHex),
);

// Some clients publish "private" lists with plaintext p-tags — read as fallback.
const plaintextListEvent = finalizeEvent(
  {
    kind: 10000,
    created_at: BASE,
    tags: [['p', KEYS.userAuthorized.pkHex]],
    content: '',
  },
  fromHex(KEYS.list.skHex),
);

// Advanced variant: NIP-78 app-specific data with a d tag.
const kind30078Event = finalizeEvent(
  {
    kind: 30078,
    created_at: BASE,
    tags: [['d', 'jellyfin-allowlist']],
    content: encryptToSelf(JSON.stringify({ allowed: [KEYS.userAuthorized.pkHex] }), BASE),
  },
  fromHex(KEYS.list.skHex),
);

// Same kind/author but OLDER and granting the unauthorized user — must be ignored.
const olderListEvent = finalizeEvent(
  {
    kind: 10000,
    created_at: BASE - 100,
    tags: [['p', KEYS.userUnauthorized.pkHex]],
    content: '',
  },
  fromHex(KEYS.list.skHex),
);

// Valid shape but authored by the wrong key — must be rejected.
const attackerListEvent = finalizeEvent(
  {
    kind: 10000,
    created_at: BASE,
    tags: [['p', KEYS.userUnauthorized.pkHex]],
    content: '',
  },
  fromHex(KEYS.attacker.skHex),
);

const allowlist = {
  generatedAt: new Date(BASE * 1000).toISOString(),
  keys: [KEYS.list, KEYS.userAuthorized, KEYS.userUnauthorized, KEYS.attacker].map(describeKey),
  cases: [
    {
      name: 'encrypted private list authorizes userAuthorized',
      event: encryptedListEvent,
      expectAuthorized: [KEYS.userAuthorized.pkHex],
      expectUnauthorized: [KEYS.userUnauthorized.pkHex, KEYS.attacker.pkHex],
      format: 'nip44-private-kind10000',
    },
    {
      name: 'plaintext fallback list authorizes userAuthorized, warns about public list',
      event: plaintextListEvent,
      expectAuthorized: [KEYS.userAuthorized.pkHex],
      expectUnauthorized: [KEYS.userUnauthorized.pkHex],
      expectPublicListWarning: true,
      format: 'plaintext-p-tags',
    },
    {
      name: 'kind 30078 d=jellyfin-allowlist authorizes userAuthorized',
      event: kind30078Event,
      expectAuthorized: [KEYS.userAuthorized.pkHex],
      format: 'nip44-private-kind30078',
    },
    {
      name: 'older replaceable event is ignored when newer exists',
      events: [olderListEvent, encryptedListEvent],
      now: BASE,
      expectAuthorized: [KEYS.userAuthorized.pkHex],
      expectUnauthorized: [KEYS.userUnauthorized.pkHex],
      format: 'newest-wins',
    },
    {
      name: 'event signed by wrong key is rejected',
      event: attackerListEvent,
      expectError: 'invalid_signature',
      format: 'bad-author',
    },
  ],
};

// ---- NIP-98 fixtures ---------------------------------------------------------

const LOGIN_URL = 'https://jellyfin.example.com/NostrAuth/Login';
const USER_AUTHORIZED_SK = fromHex(KEYS.userAuthorized.skHex);
const BODY = JSON.stringify({ deviceId: 'test-device', clientName: 'web', clientVersion: '1.0' });

const nip98Event = ({ sk, url, method, createdAt, kind = 27235, withPayload = false, tamper = false }) => {
  const tags = [['u', url], ['method', method]];
  if (withPayload) // NIP-98 payload tag = sha256 hex of the RAW request body.
  // (nostr-tools' hashPayload hashes JSON.stringify(payload) instead —
  // see the interop note in docs/design.md; we follow the spec.)
  tags.push(['payload', createHash('sha256').update(BODY).digest('hex')]);
  const ev = finalizeEvent(
    { kind, created_at: createdAt, tags, content: '' },
    fromHex(sk.skHex),
  );
  if (tamper) {
    const flipped = ev.sig.startsWith('0') ? '1' + ev.sig.slice(1) : '0' + ev.sig.slice(1);
    return { ...ev, sig: flipped };
  }
  return ev;
};

const b64 = (ev) => Buffer.from(JSON.stringify(ev), 'utf8').toString('base64');

const nip98 = {
  loginUrl: LOGIN_URL,
  requestBody: BODY,
  cases: [
    {
      name: 'valid event within freshness window is accepted',
      authorization: `Nostr ${b64(nip98Event({ sk: KEYS.userAuthorized, url: LOGIN_URL, method: 'POST', createdAt: BASE }))}`,
      now: BASE + 10,
      expect: 'accept',
      expectPubkey: KEYS.userAuthorized.pkHex,
    },
    {
      name: 'created_at older than 60s is rejected',
      authorization: `Nostr ${b64(nip98Event({ sk: KEYS.userAuthorized, url: LOGIN_URL, method: 'POST', createdAt: BASE - 120 }))}`,
      now: BASE,
      expect: 'reject',
      expectReason: 'expired_event',
    },
    {
      name: 'created_at in the future beyond tolerance is rejected',
      authorization: `Nostr ${b64(nip98Event({ sk: KEYS.userAuthorized, url: LOGIN_URL, method: 'POST', createdAt: BASE + 120 }))}`,
      now: BASE,
      expect: 'reject',
      expectReason: 'expired_event',
    },
    {
      name: 'u tag not matching request URL is rejected',
      authorization: `Nostr ${b64(nip98Event({ sk: KEYS.userAuthorized, url: 'https://jellyfin.example.com/other', method: 'POST', createdAt: BASE }))}`,
      now: BASE + 10,
      expect: 'reject',
      expectReason: 'url_mismatch',
    },
    {
      name: 'method tag not matching HTTP method is rejected',
      authorization: `Nostr ${b64(nip98Event({ sk: KEYS.userAuthorized, url: LOGIN_URL, method: 'GET', createdAt: BASE }))}`,
      now: BASE + 10,
      expect: 'reject',
      expectReason: 'method_mismatch',
    },
    {
      name: 'non-27235 kind is rejected',
      authorization: `Nostr ${b64(nip98Event({ sk: KEYS.userAuthorized, url: LOGIN_URL, method: 'POST', createdAt: BASE, kind: 27236 }))}`,
      now: BASE + 10,
      expect: 'reject',
      expectReason: 'invalid_event',
    },
    {
      name: 'tampered signature is rejected',
      authorization: `Nostr ${b64(nip98Event({ sk: KEYS.userAuthorized, url: LOGIN_URL, method: 'POST', createdAt: BASE, tamper: true }))}`,
      now: BASE + 10,
      expect: 'reject',
      expectReason: 'invalid_event',
    },
    {
      name: 'payload hash matching request body is accepted',
      authorization: `Nostr ${b64(nip98Event({ sk: KEYS.userAuthorized, url: LOGIN_URL, method: 'POST', createdAt: BASE, withPayload: true }))}`,
      requestBody: BODY,
      now: BASE + 10,
      expect: 'accept',
      expectPubkey: KEYS.userAuthorized.pkHex,
    },
    {
      name: 'payload hash not matching request body is rejected',
      authorization: `Nostr ${b64(nip98Event({ sk: KEYS.userAuthorized, url: LOGIN_URL, method: 'POST', createdAt: BASE, withPayload: true }))}`,
      requestBody: JSON.stringify({ deviceId: 'different' }),
      now: BASE + 10,
      expect: 'reject',
      expectReason: 'invalid_event',
    },
  ],
};

// Login by the unauthorized user (valid NIP-98) — allowlist check must reject.
nip98.cases.push({
  name: 'valid event from a non-allowlisted pubkey passes NIP-98 but is rejected by authorization',
  authorization: `Nostr ${b64(nip98Event({ sk: KEYS.userUnauthorized, url: LOGIN_URL, method: 'POST', createdAt: BASE }))}`,
  now: BASE + 10,
  expect: 'accept',
  expectPubkey: KEYS.userUnauthorized.pkHex,
  expectNotInAllowlist: true,
});

writeFileSync('allowlist.json', JSON.stringify(allowlist, null, 2) + '\n');
writeFileSync('nip98.json', JSON.stringify(nip98, null, 2) + '\n');
console.log('wrote allowlist.json and nip98.json');
