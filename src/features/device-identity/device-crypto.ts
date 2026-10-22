import type { StoredDeviceIdentity } from "@/offline/types";
import { DeviceIdentityError } from "@/features/device-identity/device-identity-error";

const algorithm = { name: "ECDSA", namedCurve: "P-256" } as const;

function bytesToBase64(bytes: Uint8Array) {
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary);
}

function base64ToBytes(value: string) {
  return Uint8Array.from(atob(value), (character) => character.charCodeAt(0));
}

function bytesToHex(bytes: Uint8Array) {
  return Array.from(bytes, (byte) => byte.toString(16).padStart(2, "0")).join("");
}

export async function publicKeyFingerprint(publicKeySpki: string) {
  try {
    const digest = await crypto.subtle.digest("SHA-256", base64ToBytes(publicKeySpki));
    return bytesToHex(new Uint8Array(digest));
  } catch {
    throw new DeviceIdentityError("A chave publica local esta corrompida. Solicite um novo cadastro.");
  }
}

export async function validateStoredDeviceIdentity(
  identity: StoredDeviceIdentity,
  expectedFingerprint: string | null = identity.publicKeyFingerprint,
) {
  if (!globalThis.crypto?.subtle) {
    throw new DeviceIdentityError("Este navegador nao oferece validacao criptografica segura.");
  }
  const key = identity.privateKey;
  const keyAlgorithm = key?.algorithm as EcKeyAlgorithm | undefined;
  if (!key
    || key.type !== "private"
    || key.extractable
    || !key.usages.includes("sign")
    || keyAlgorithm?.name !== "ECDSA"
    || keyAlgorithm.namedCurve !== "P-256") {
    throw new DeviceIdentityError("A chave privada local foi perdida ou e invalida. Solicite um novo cadastro.");
  }

  const fingerprint = await publicKeyFingerprint(identity.publicKeySpki);
  if (expectedFingerprint && fingerprint !== expectedFingerprint.trim().toLowerCase()) {
    throw new DeviceIdentityError("A identidade local nao corresponde ao registro do servidor. Solicite um novo cadastro.");
  }

  try {
    const publicKey = await crypto.subtle.importKey(
      "spki",
      base64ToBytes(identity.publicKeySpki),
      algorithm,
      false,
      ["verify"],
    );
    const probe = new TextEncoder().encode("TRANSJAP-DEVICE-IDENTITY-CHECK\nv1");
    const signature = await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, key, probe);
    const matches = await crypto.subtle.verify(
      { name: "ECDSA", hash: "SHA-256" },
      publicKey,
      signature,
      probe,
    );
    if (!matches) throw new Error("key mismatch");
  } catch {
    throw new DeviceIdentityError("A chave privada local nao corresponde a chave publica registrada. Solicite um novo cadastro.");
  }
  return fingerprint;
}

export async function generateDeviceIdentity(
  localDeviceId: string,
  displayName: string,
  now = new Date(),
): Promise<StoredDeviceIdentity> {
  if (!globalThis.crypto?.subtle) {
    throw new Error("Este navegador nao oferece armazenamento criptografico seguro.");
  }
  const pair = await crypto.subtle.generateKey(algorithm, false, ["sign", "verify"]);
  const exportedPublicKey = await crypto.subtle.exportKey("spki", pair.publicKey);
  const timestamp = now.toISOString();
  return {
    key: "current",
    schemaVersion: 1,
    localDeviceId,
    registeredDeviceId: null,
    displayName: displayName.trim(),
    platform: "WEB_PWA",
    keyAlgorithm: "ECDSA_P256_SHA256",
    publicKeySpki: bytesToBase64(new Uint8Array(exportedPublicKey)),
    privateKey: pair.privateKey,
    publicKeyFingerprint: null,
    status: "UNREGISTERED",
    lastValidatedAt: null,
    createdAt: timestamp,
    updatedAt: timestamp,
  };
}

export async function signDeviceChallenge(privateKey: CryptoKey, payload: string) {
  if (privateKey.type !== "private" || privateKey.extractable || !privateKey.usages.includes("sign")) {
    throw new Error("A chave privada local nao atende ao contrato de seguranca.");
  }
  const signature = await crypto.subtle.sign(
    { name: "ECDSA", hash: "SHA-256" },
    privateKey,
    new TextEncoder().encode(payload),
  );
  return bytesToBase64(new Uint8Array(signature));
}
