import { apiFetch } from "@/services/api-client";
import type { WorkSite } from "@/types/domain";

export async function getWorkSites(): Promise<readonly WorkSite[]> {
  return apiFetch<WorkSite[]>("/api/v1/worksites");
}
