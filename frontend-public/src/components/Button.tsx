import Link from "next/link";

interface Props {
  href: string;
  children: React.ReactNode;
  variant?: "primary" | "trust" | "secondary";
  className?: string;
}

export function Button({ href, children, variant = "primary", className = "" }: Props) {
  const variantClass =
    variant === "trust" ? "btn-trust" : variant === "secondary" ? "btn-secondary" : "btn-primary";

  return (
    <Link href={href} className={`btn ${variantClass} ${className}`}>
      {children}
    </Link>
  );
}
