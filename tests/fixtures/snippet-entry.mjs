// Entry for the browser bundle vendored into the plugin (Web/nostr.mjs):
// only the nostr-tools surface the login page needs — the full library index
// pulls in the WASM-search stack and other dead weight.
import * as core from 'nostr-tools/core';
import * as nip19 from 'nostr-tools/nip19';
import * as nip44 from 'nostr-tools/nip44';
import * as nip46 from 'nostr-tools/nip46';
import * as pure from 'nostr-tools/pure';
import * as utils from 'nostr-tools/utils';

import qrcode from 'qrcode-generator';

export { core, nip19, nip44, nip46, pure, utils, qrcode };
