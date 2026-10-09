import { z } from "zod";

export const readingsFilterSchema = z
  .object({
    startDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/).or(z.literal("")),
    endDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/).or(z.literal("")),
    fleetNumber: z.string().regex(/^\d+$/).or(z.literal("")),
    workSiteId: z.string().uuid().or(z.literal("")),
    status: z
      .enum(["VALIDATED", "PENDING", "SUSPECT", "CORRECTED", "REJECTED"])
      .or(z.literal("")),
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

export const emptyReadingsFilters: ReadingsFilterValues = {
  endDate: "",
  fleetNumber: "",
  startDate: "",
  status: "",
  workSiteId: "",
};
