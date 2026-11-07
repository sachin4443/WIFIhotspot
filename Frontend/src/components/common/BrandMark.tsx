/** NXT Hotspot logo mark (Wi-Fi arcs). */
export function BrandMark({ size = 36 }: { size?: number }) {
  return (
    <svg width={size} height={size} viewBox="0 0 32 32" aria-hidden="true" className="flex-shrink-0">
      <rect width="32" height="32" rx="8" fill="#2563eb" />
      <path
        d="M8 13.5a11.3 11.3 0 0 1 16 0M11 16.8a7 7 0 0 1 10 0M14 20.1a2.8 2.8 0 0 1 4 0"
        fill="none"
        stroke="#fff"
        strokeWidth="2.2"
        strokeLinecap="round"
      />
      <circle cx="16" cy="23.5" r="1.6" fill="#fff" />
    </svg>
  );
}
