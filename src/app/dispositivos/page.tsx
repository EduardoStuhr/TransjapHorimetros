import type { Metadata } from "next";

import { PageHeading } from "@/components/shared/page-heading";
import { DeviceManagement } from "@/features/device-identity/device-management";

export const metadata: Metadata = { title: "Dispositivos" };

export default function DevicesPage() {
  return <div className="space-y-5">
    <PageHeading title="Dispositivos confiáveis" description="Autorize, identifique e revogue dispositivos usados nas coletas de campo." />
    <DeviceManagement />
  </div>;
}
