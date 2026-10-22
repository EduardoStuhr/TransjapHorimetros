import { proxyDeviceRequest } from "@/app/api/devices/proxy";

export async function GET(request: Request, { params }: RouteContext<"/api/devices/[deviceId]/history">) {
  const { deviceId } = await params;
  return proxyDeviceRequest(`/api/v1/devices/${encodeURIComponent(deviceId)}/history`, request, "GET");
}
