import { proxyDeviceRequest } from "@/app/api/devices/proxy";

export async function POST(request: Request) {
  return proxyDeviceRequest("/api/v1/devices/challenge/verify", request, "POST");
}
