import {
  BellRing,
  ChartNoAxesCombined,
  FileChartColumn,
  Gauge,
  HardHat,
  MapPinned,
} from "lucide-react";

export const navigationItems = [
  { label: "Dashboard", href: "/", icon: ChartNoAxesCombined, roles: ["SUPERVISOR", "MANAGER", "ADMIN"] },
  { label: "Frota", href: "/frota", icon: HardHat, roles: null },
  { label: "Horímetros", href: "/horimetros", icon: Gauge, roles: null },
  { label: "Obras", href: "/obras", icon: MapPinned, roles: null },
  { label: "Alertas", href: "/alertas", icon: BellRing, roles: ["SUPERVISOR", "MANAGER", "ADMIN"] },
  { label: "Relatórios", href: "/relatorios", icon: FileChartColumn, roles: ["SUPERVISOR", "MANAGER", "ADMIN"] },
] as const;

export function canAccessNavigationItem(
  roles: readonly string[],
  item: (typeof navigationItems)[number],
) {
  return item.roles === null || item.roles.some((role) => roles.includes(role));
}

export function isNavigationItemActive(pathname: string, href: string) {
  if (href === "/") {
    return pathname === "/";
  }

  return pathname.startsWith(href);
}
