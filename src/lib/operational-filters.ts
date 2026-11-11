import { readingsFilterSchema, emptyReadingsFilters } from "@/schemas/readings-filter.schema";

export function parseOperationalFilters(params: Record<string, string | string[] | undefined>) {
  const input = Object.fromEntries(Object.keys(emptyReadingsFilters).map(key => [key, typeof params[key] === "string" ? params[key] : ""]));
  return readingsFilterSchema.parse(input);
}

export function filterSearch(filters: Record<string, string | undefined>) {
  return new URLSearchParams(Object.entries(filters).filter((entry): entry is [string, string] => Boolean(entry[1]))).toString();
}
