import type { Metadata } from "next";

import { ReadingCaptureForm } from "@/features/offline/reading-capture-form";
import { DeviceIdentityGate } from "@/features/device-identity/device-identity-gate";

export const metadata: Metadata = { title: "Registrar leitura" };

export default function ReadingCapturePage() {
  return <DeviceIdentityGate><ReadingCaptureForm /></DeviceIdentityGate>;
}
