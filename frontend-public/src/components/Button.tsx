import Link from "next/link";

interface Props {
  href: string;
  children: React.ReactNode;
  variant?: "primary" | "brand" | "secondary" | "ghost";
  className?: string;
  external?: boolean;
}

export function Button({ href, children, variant = "brand", className = "", external }: Props) {
  const variantClass =
    variant === "primary"
      ? "btn-primary"
      : variant === "secondary"
        ? "btn-secondary"
        : variant === "ghost"
          ? "btn-ghost"
          : "btn-brand";

  if (external) {
    return (
      <a href={href} className={`btn ${variantClass} ${className}`} target="_blank" rel="noopener noreferrer">
        {children}
      </a>
    );
  }

  return (
    <Link href={href} className={`btn ${variantClass} ${className}`}>
      {children}
    </Link>
  );
}
