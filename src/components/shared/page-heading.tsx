import type { ReactNode } from "react";

interface PageHeadingProps {
  eyebrow?: string;
  title: string;
  description: string;
  action?: ReactNode;
}

export function PageHeading({
  eyebrow,
  title,
  description,
  action,
}: PageHeadingProps) {
  return (
    <div className="flex flex-col justify-between gap-4 border-b border-slate-200 pb-5 sm:flex-row sm:items-end">
      <div className="max-w-3xl">
        {eyebrow ? (
          <p className="mb-2 text-xs font-bold uppercase tracking-[0.16em] text-[#2c5b9e]">
            {eyebrow}
          </p>
        ) : null}
        <h2 className="text-2xl font-bold tracking-tight text-slate-950 sm:text-3xl">
          {title}
        </h2>
        <p className="mt-2 text-base text-slate-600">{description}</p>
      </div>
      {action ? <div className="shrink-0">{action}</div> : null}
    </div>
  );
}
