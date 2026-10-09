import { z } from "zod";

export const workSiteSchema = z.object({
  active: z.boolean(),
  code: z
    .string()
    .trim()
    .min(1, "Informe o código da obra.")
    .max(40, "O código deve ter no máximo 40 caracteres."),
  id: z.string().uuid().optional(),
  name: z
    .string()
    .trim()
    .min(1, "Informe o nome da obra.")
    .max(160, "O nome deve ter no máximo 160 caracteres."),
});

export interface WorkSiteActionState {
  fieldErrors?: {
    code?: string[];
    name?: string[];
  };
  message: string;
  status: "idle" | "error" | "success";
  submissionId?: string;
}
