import { describe, expect, it } from "vitest";

import { canConfigureMachineMeterUnit } from "./machine-meter-unit-form";

describe("configuração administrativa de MeterUnit", () => {
  it.each(["ADMIN", "MANAGER"])("permite o papel %s", (role) => {
    expect(canConfigureMachineMeterUnit([role])).toBe(true);
  });

  it.each(["OPERATOR", "SUPERVISOR", "ADMINISTRATIVE"])("não permite o papel %s", (role) => {
    expect(canConfigureMachineMeterUnit([role])).toBe(false);
  });
});
