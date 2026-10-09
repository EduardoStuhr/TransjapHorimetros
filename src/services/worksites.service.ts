import { apiFetch } from "@/services/api-client";
import type { WorkSite } from "@/types/domain";

export interface WorkSiteInput {
  active: boolean;
  code: string;
  name: string;
}

export async function getWorkSites(): Promise<readonly WorkSite[]> {
  return apiFetch<WorkSite[]>("/api/v1/worksites");
}

export async function createWorkSite(
  input: WorkSiteInput,
): Promise<WorkSite> {
  return apiFetch<WorkSite>("/api/v1/worksites", {
    body: input,
    method: "POST",
  });
}

export async function updateWorkSite(
  id: string,
  input: WorkSiteInput,
): Promise<WorkSite> {
  return apiFetch<WorkSite>(`/api/v1/worksites/${encodeURIComponent(id)}`, {
    body: input,
    method: "PUT",
  });
}
