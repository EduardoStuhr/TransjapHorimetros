import { afterEach, expect, it, vi } from "vitest";
import { GET } from "@/app/api/version/route";

afterEach(() => vi.unstubAllEnvs());

it("exposes only the build identifier with caching disabled", async () => {
  const sha = "a".repeat(40);
  vi.stubEnv("BUILD_COMMIT_SHA", sha);
  const response = GET();
  expect(await response.json()).toEqual({ commit: sha });
  expect(response.headers.get("Cache-Control")).toBe("no-store");
});
