import { describe, expect, it } from "vitest";

import { generateDeviceIdentity, signDeviceChallenge } from "@/features/device-identity/device-crypto";

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
});
