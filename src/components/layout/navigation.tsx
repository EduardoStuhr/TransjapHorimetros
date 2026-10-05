import {
  BellRing,
  ChartNoAxesCombined,
  FileChartColumn,
  Gauge,
  HardHat,
  MapPinned,
} from "lucide-react";

export const navigationItems = [
  { label: "Dashboard", href: "/", icon: ChartNoAxesCombined },
  { label: "Frota", href: "/frota", icon: HardHat },
  { label: "Horímetros", href: "/horimetros", icon: Gauge },
  { label: "Obras", href: "/obras", icon: MapPinned },
  { label: "Alertas", href: "/alertas", icon: BellRing },
  { label: "Relatórios", href: "/relatorios", icon: FileChartColumn },
] as const;

export function isNavigationItemActive(pathname: string, href: string) {
  if (href === "/") {
    return pathname === "/";
  }

  return pathname.startsWith(href);
}
