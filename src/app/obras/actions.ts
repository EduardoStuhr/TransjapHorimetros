"use server";

import { revalidatePath } from "next/cache";

import {
  workSiteSchema,
  type WorkSiteActionState,
} from "@/schemas/worksite.schema";
import {
  createWorkSite,
  updateWorkSite,
} from "@/services/worksites.service";

export async function saveWorkSiteAction(
  _previousState: WorkSiteActionState,
  formData: FormData,
): Promise<WorkSiteActionState> {
  const id = formData.get("id");
  const result = workSiteSchema.safeParse({
    active: formData.get("active") === "on",
    code: formData.get("code"),
    id: typeof id === "string" && id ? id : undefined,
    name: formData.get("name"),
  });

  if (!result.success) {
    const fieldErrors = result.error.flatten().fieldErrors;

    return {
      fieldErrors: {
        code: fieldErrors.code,
        name: fieldErrors.name,
      },
      message: "Revise os campos destacados.",
      status: "error",
    };
  }

  const { id: workSiteId, ...input } = result.data;

  try {
    if (workSiteId) {
      await updateWorkSite(workSiteId, input);
    } else {
      await createWorkSite(input);
    }

    revalidatePath("/obras");

    return {
      message: workSiteId
        ? "Obra atualizada com sucesso."
        : "Obra cadastrada com sucesso.",
      status: "success",
      submissionId: crypto.randomUUID(),
    };
  } catch (error) {
    return {
      message:
        error instanceof Error
          ? error.message
          : "Não foi possível salvar a obra.",
      status: "error",
    };
  }
}
