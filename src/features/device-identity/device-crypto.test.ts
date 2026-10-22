import { describe, expect, it } from "vitest";

import {
  generateDeviceIdentity,
  publicKeyFingerprint,
  signDeviceChallenge,
  validateStoredDeviceIdentity,
} from "@/features/device-identity/device-crypto";
import { DeviceIdentityError } from "@/features/device-identity/device-identity-error";

function base64ToBytes(value: string) {
  return Uint8Array.from(atob(value), (character) => character.charCodeAt(0));
}

describe("device crypto", () => {
  it("gera chave privada P-256 nao exportavel e assinatura IEEE P1363 verificavel", async () => {
    const identity = await generateDeviceIdentity("web-test", "Tablet teste", new Date("2026-10-22T12:00:00Z"));
    expect(identity.privateKey.type).toBe("private");
    expect(identity.privateKey.extractable).toBe(false);
    await expect(crypto.subtle.exportKey("pkcs8", identity.privateKey)).rejects.toThrow();

    const payload = "TRANSJAP-DEVICE-CHALLENGE\nv1\nchallenge";
    const signature = base64ToBytes(await signDeviceChallenge(identity.privateKey, payload));
    const publicKey = await crypto.subtle.importKey("spki", base64ToBytes(identity.publicKeySpki),
      { name: "ECDSA", namedCurve: "P-256" }, false, ["verify"]);
    expect(signature.byteLength).toBe(64);
    await expect(crypto.subtle.verify({ name: "ECDSA", hash: "SHA-256" }, publicKey, signature,
      new TextEncoder().encode(payload))).resolves.toBe(true);
  });

  it("valida a chave privada local e o fingerprint SHA-256 do SPKI", async () => {
    const identity = await generateDeviceIdentity("web-test", "Tablet teste");
    const fingerprint = await publicKeyFingerprint(identity.publicKeySpki);

    await expect(validateStoredDeviceIdentity(identity, fingerprint)).resolves.toBe(fingerprint);
  });

  it("bloqueia chave privada ausente ou fora do contrato seguro", async () => {
    const identity = await generateDeviceIdentity("web-test", "Tablet teste");
    const invalid = {
      ...identity,
      privateKey: {
        type: "private",
        extractable: true,
        usages: ["sign"],
        algorithm: { name: "ECDSA", namedCurve: "P-256" },
      } as CryptoKey,
    };

    await expect(validateStoredDeviceIdentity(invalid)).rejects.toBeInstanceOf(DeviceIdentityError);
  });

  it("bloqueia fingerprint local diferente do servidor", async () => {
    const identity = await generateDeviceIdentity("web-test", "Tablet teste");

    await expect(validateStoredDeviceIdentity(identity, "00".repeat(32)))
      .rejects.toThrow("nao corresponde ao registro do servidor");
  });

  it("bloqueia chave privada valida que nao corresponde a chave publica armazenada", async () => {
    const first = await generateDeviceIdentity("web-first", "Tablet 1");
    const second = await generateDeviceIdentity("web-second", "Tablet 2");
    const mismatched = { ...first, privateKey: second.privateKey };

    await expect(validateStoredDeviceIdentity(mismatched, null))
      .rejects.toThrow("nao corresponde a chave publica registrada");
  });
});
