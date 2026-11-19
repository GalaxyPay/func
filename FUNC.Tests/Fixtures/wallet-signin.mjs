// Regenerates wallet-signin.json: ARC-60 (Sign-in with Algorand) responses as Lute and
// Pera return them, signed with fixed test keys. Run: node wallet-signin.mjs > wallet-signin.json
//
// ARC-60 signs ed25519(sha256(data) || sha256(authenticatorData)), where data is the
// SIWA JSON and authenticatorData is sha256(domain), the page's host.
import crypto from "node:crypto";

const sha256 = (b) => crypto.createHash("sha256").update(b).digest();
const b32 = (buf) => {
  const abc = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
  let bits = 0, value = 0, out = "";
  for (const byte of buf) {
    value = (value << 8) | byte; bits += 8;
    while (bits >= 5) { out += abc[(value >>> (bits - 5)) & 31]; bits -= 5; }
  }
  if (bits > 0) out += abc[(value << (5 - bits)) & 31];
  return out;
};
const address = (pk) => b32(Buffer.concat([pk, crypto.createHash("sha512-256").update(pk).digest().subarray(28)]));
const key = (seedByte) => {
  const seed = Buffer.alloc(32, seedByte);
  const priv = crypto.createPrivateKey({ key: Buffer.concat([Buffer.from("302e020100300506032b657004220420", "hex"), seed]), format: "der", type: "pkcs8" });
  const pk = crypto.createPublicKey(priv).export({ format: "der", type: "spki" }).subarray(12);
  return { priv, pk, addr: address(pk) };
};

const nonce = "a3f1c2d4e5b60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90";
const domain = "192.168.1.5:3537";

function proof(account, signer) {
  const siwa = {
    domain,
    account_address: account,
    uri: `https://${domain}`,
    version: "1",
    statement: "Sign in to FUNC",
    nonce,
    "issued-at": "2026-11-19T12:00:00.000Z",
    "expiration-time": "2099-01-01T00:00:00.000Z",
    chain_id: "283",
    type: "ed25519",
  };
  const data = Buffer.from(JSON.stringify(siwa));
  const authenticatorData = sha256(domain);
  const signature = crypto.sign(null, Buffer.concat([sha256(data), sha256(authenticatorData)]), signer.priv);
  return {
    address: account,
    data: data.toString("base64"),
    signer: signer.pk.toString("base64"),
    domain,
    authenticatorData: authenticatorData.toString("base64"),
    signature: signature.toString("base64"),
  };
}

const owner = key(1);
const rekeyed = key(2);
const authKey = key(3);
console.log(JSON.stringify({
  nonce,
  domain,
  direct: proof(owner.addr, owner),
  rekeyed: { authAddr: authKey.addr, ...proof(rekeyed.addr, authKey) },
}, null, 2));
