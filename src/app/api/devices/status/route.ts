import { proxyDeviceRequest } from "@/app/api/devices/proxy";

export async function GET(request: Request) {
  const search = new URL(request.url).search;
  return proxyDeviceRequest(`/api/v1/devices/status${search}`, request, "GET");
}
