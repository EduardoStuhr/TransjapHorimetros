import { proxyDeviceRequest } from "@/app/api/devices/proxy";

export async function POST(request: Request, { params }: RouteContext<"/api/devices/[deviceId]/authorize">) {
  const { deviceId } = await params;
  return proxyDeviceRequest(`/api/v1/devices/${encodeURIComponent(deviceId)}/authorize`, request, "POST");
}
