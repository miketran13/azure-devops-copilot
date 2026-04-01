import * as React from "react";

/**
 * Animated SVG Pac-Man sprite (facing right).
 * Uses React state to toggle between open/closed mouth every 200 ms,
 * so it works without any CSS keyframe dependency.
 *
 * @param size   Pixel size (width = height). Default 16.
 * @param animate Whether to run the chomp animation. Default true.
 */
export function PacManSprite({
  size = 16,
  animate = true,
}: {
  size?: number;
  animate?: boolean;
}): React.ReactElement {
  const [open, setOpen] = React.useState(true);

  React.useEffect(() => {
    if (!animate) return;
    const id = setInterval(() => setOpen((o) => !o), 200);
    return () => clearInterval(id);
  }, [animate]);

  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      style={{ display: "block", flexShrink: 0 }}
      aria-hidden="true"
    >
      {open ? (
        /*
         * Open mouth: start at center, line to top lip (at -30° from horizontal),
         * arc counterclockwise the long way (300°) to bottom lip, close to center.
         * This gives a 60° mouth opening facing right.
         */
        <path d="M12 12 L20.66 7 A10 10 0 1 0 20.66 17 Z" fill="#ffff00" />
      ) : (
        /* Closed: full yellow circle */
        <circle cx="12" cy="12" r="10" fill="#ffff00" />
      )}
    </svg>
  );
}

/**
 * Red ghost (BLINKY) SVG icon with a gentle float animation.
 *
 * @param size  Pixel size. Default 16.
 * @param color Ghost body colour. Default Blinky red #ff0000.
 */
export function GhostIcon({
  size = 16,
  color = "#ff0000",
}: {
  size?: number;
  color?: string;
}): React.ReactElement {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      style={{
        display: "block",
        flexShrink: 0,
        animation: "ghost-float 1.2s ease-in-out infinite",
      }}
      aria-hidden="true"
    >
      <path
        d="M4 22V12C4 7.58 7.58 4 12 4s8 3.58 8 8v10l-2.5-2.5L15 22l-3-3-3 3-2.5-2.5L4 22z"
        fill={color}
      />
      {/* Pupils */}
      <circle cx="9" cy="11" r="1.5" fill="#ffffff" />
      <circle cx="15" cy="11" r="1.5" fill="#ffffff" />
      <circle cx="9.6" cy="11.5" r="0.7" fill="#222" />
      <circle cx="15.6" cy="11.5" r="0.7" fill="#222" />
    </svg>
  );
}
