import * as React from "react";

type ButtonVariant = "default" | "outline" | "ghost";

type ButtonSize = "default" | "lg";

interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  asChild?: boolean;
  variant?: ButtonVariant;
  size?: ButtonSize;
}

const baseStyles =
  "inline-flex items-center justify-center rounded-full text-sm font-medium transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500/40 disabled:pointer-events-none disabled:opacity-50";

const variantStyles: Record<ButtonVariant, string> = {
  default: "bg-neutral-900 text-white hover:bg-neutral-800",
  outline: "border border-neutral-200 bg-white text-neutral-900 hover:bg-neutral-50",
  ghost: "text-neutral-700 hover:bg-neutral-100",
};

const sizeStyles: Record<ButtonSize, string> = {
  default: "h-10 px-5",
  lg: "h-11 px-6 text-base",
};

function ButtonRoot(
  {
    asChild,
    className,
    variant = "default",
    size = "default",
    children,
    ...props
  }: ButtonProps,
  ref: React.Ref<HTMLButtonElement>
) {
  const classes = [
    baseStyles,
    variantStyles[variant],
    sizeStyles[size],
    className,
  ]
    .filter(Boolean)
    .join(" ");

  if (asChild && children) {
    const child = React.Children.only(children) as React.ReactElement;
    return React.cloneElement(child, {
      ...props,
      className: [classes, child.props.className].filter(Boolean).join(" "),
    });
  }

  return (
    <button ref={ref} className={classes} {...props}>
      {children}
    </button>
  );
}

export const Button = React.forwardRef(ButtonRoot);
Button.displayName = "Button";
