import type { StoredDeviceIdentity } from "@/offline/types";

const algorithm = { name: "ECDSA", namedCurve: "P-256" } as const;

function bytesToBase64(bytes: Uint8Array) {
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary);
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
