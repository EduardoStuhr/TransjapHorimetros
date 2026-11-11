"use server";
import { revalidatePath } from "next/cache";
import { unstable_rethrow } from "next/navigation";
import { apiFetch } from "@/services/api-client";

export async function reviewReading(
  _state: string,
  form: FormData,
): Promise<string> {
  const id = String(form.get("id"));
  const reason = String(form.get("reason") ?? "").trim();
  if (reason.length < 3 || reason.length > 1000)
    return "Informe justificativa de 3 a 1000 caracteres.";
  try {
    await apiFetch(
      "/api/v1/operations/readings/" + encodeURIComponent(id) + "/review",
      {
        method: "POST",
        body: { approved: form.get("approved") === "true", reason },
      },
    );
    revalidatePath("/horimetros/" + id);
    revalidatePath("/");
    return "Decisão registrada.";
  } catch (error) {
    unstable_rethrow(error);
    return error instanceof Error
      ? error.message
      : "Não foi possível registrar a decisão.";
  }
}
