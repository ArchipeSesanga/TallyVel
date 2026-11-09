import { Button as ButtonPrimitive } from "@base-ui/react/button"
import { cva, type VariantProps } from "class-variance-authority"
import { cn } from "cn"

// TallyVel buttons: 44px tall (40px for compact ones in a row), 12px corners, 15px semibold text, flat (no shadow,
// no gradient, no hover animation), and a visible 2px ring with an offset when focused from the keyboard.
const buttonVariants = cva(
  "group/button inline-flex shrink-0 items-center justify-center gap-2 rounded-xl border border-transparent text-[15px] font-semibold whitespace-nowrap outline-none select-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 aria-invalid:border-destructive [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-[18px]",
  {
    variants: {
      variant: {
        // primary
        default: "bg-primary text-primary-foreground hover:bg-primary/90",
        outline: "border-input bg-card text-foreground hover:bg-muted",
        secondary: "border-input bg-muted text-foreground hover:bg-divider",
        ghost: "text-foreground hover:bg-muted",
        destructive: "bg-destructive/10 text-destructive hover:bg-destructive/20",
        link: "text-success",
      },
      size: {
        default: "min-h-11 px-[18px]",
        xs: "min-h-8 rounded-[10px] px-2.5 text-[13px]",
        sm: "min-h-10 px-3.5", // compact, for controls inside a row
        lg: "min-h-11 px-[18px]",
        icon: "size-11",
        "icon-xs": "size-8 rounded-[10px]",
        "icon-sm": "size-10",
        "icon-lg": "size-11",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  }
)

function Button({
  className,
  variant = "default",
  size = "default",
  ...props
}: ButtonPrimitive.Props & VariantProps<typeof buttonVariants>) {
  return (
    <ButtonPrimitive
      data-slot="button"
      className={cn(buttonVariants({ variant, size, className }))}
      {...props}
    />
  )
}

export { Button, buttonVariants }
