import { machines } from "@/data/machines";
import type { Machine } from "@/types/domain";

export async function getMachines(): Promise<readonly Machine[]> {
  return machines;
}

export async function getMachineByFleetNumber(
  fleetNumber: number,
): Promise<Machine | undefined> {
  return machines.find((machine) => machine.fleetNumber === fleetNumber);
}
