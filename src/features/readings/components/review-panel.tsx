"use client";
import { useAuth } from "@/components/auth/auth-provider";
import { useActionState } from "react";
import { reviewReading } from "@/app/horimetros/[readingId]/review-actions";
import type { ReadingReview } from "@/services/operations.service";

export function ReviewPanel({
  id,
  review,
  canReview,
}: {
  id: string;
  review: ReadingReview | null;
  canReview: boolean;
}) {
  const { user } = useAuth();
  const allowed = user.roles.some((role) =>
    ["ADMIN", "MANAGER", "SUPERVISOR", "ADMINISTRATIVE"].includes(role),
  );
  const [message, action, pending] = useActionState(reviewReading, "");
  return (
    <section className="space-y-3 rounded border bg-white p-4">
      <h2 className="font-bold">Revisão administrativa</h2>
      {review ? (
        <div>
          <p>
            {review.approved ? "Aprovada" : "Rejeitada"} por {review.actorEmail}{" "}
            em {new Date(review.createdAt).toLocaleString("pt-BR")}
          </p>
          <p>{review.reason}</p>
        </div>
      ) : canReview && allowed ? (
        <form
          action={action}
          className="space-y-3"
          onSubmit={(e) => {
            if (!window.confirm("Confirmar esta decisão administrativa?"))
              e.preventDefault();
          }}
        >
          <input type="hidden" name="id" value={id} />
          <label className="block">
            Justificativa
            <textarea
              name="reason"
              required
              minLength={3}
              maxLength={1000}
              className="block w-full rounded border p-2"
            />
          </label>
          <div className="flex gap-3">
            <button
              className="rounded bg-[#173b67] p-2 text-white"
              disabled={pending}
              name="approved"
              value="true"
            >
              Aprovar
            </button>
            <button
              className="rounded border p-2"
              disabled={pending}
              name="approved"
              value="false"
            >
              Rejeitar
            </button>
          </div>
        </form>
      ) : (
        <p>Esta leitura não aguarda revisão.</p>
      )}
      <p role="status">{message}</p>
      <p className="text-sm">
        A decisão fica vinculada ao registro original. Valores e diferenças
        históricas são preservados.
      </p>
    </section>
  );
}
