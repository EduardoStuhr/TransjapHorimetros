import { proxyDeviceRequest } from "@/app/api/devices/proxy";

export async function PATCH(request: Request, { params }: RouteContext<"/api/devices/[deviceId]">) {
  const { deviceId } = await params;
  return proxyDeviceRequest(`/api/v1/devices/${encodeURIComponent(deviceId)}`, request, "PATCH");
}
