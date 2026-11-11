"use server";
import { revalidatePath } from "next/cache";
import { unstable_rethrow } from "next/navigation";
import { apiFetch } from "@/services/api-client";

export async function reviewAlert(_state: string, form: FormData) {
  try {
    await apiFetch(
      "/api/v1/operations/anomalies/" +
        encodeURIComponent(String(form.get("id"))) +
        "/review",
      {
        method: "POST",
        body: {
          expectedStatus: form.get("expectedStatus"),
          status: form.get("status"),
          reason: form.get("reason"),
        },
      },
    );
    revalidatePath("/alertas");
    revalidatePath("/");
    return "Revisão registrada.";
  } catch (error) {
    unstable_rethrow(error);
    return error instanceof Error ? error.message : "Não foi possível revisar.";
  }
}
