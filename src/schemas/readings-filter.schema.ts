import { z } from "zod";

export const readingsFilterSchema = z
  .object({
    startDate: z.string(),
    endDate: z.string(),
    fleetNumber: z.string(),
    model: z.string(),
    workSite: z.string(),
    status: z.string(),
  })
  .refine(
    (values) =>
      !values.startDate ||
      !values.endDate ||
      new Date(values.startDate) <= new Date(values.endDate),
    {
      message: "A data inicial deve ser anterior à data final.",
      path: ["endDate"],
    },
  );

export type ReadingsFilterValues = z.infer<typeof readingsFilterSchema>;
