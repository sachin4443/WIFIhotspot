import { useState } from 'react';
import { FiCheck, FiCopy } from 'react-icons/fi';

export function CopyButton({ text, label = 'Copy' }: { text: string; label?: string }) {
  const [copied, setCopied] = useState(false);
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(text);
    } catch {
      const ta = document.createElement('textarea');
      ta.value = text;
      document.body.appendChild(ta);
      ta.select();
      document.execCommand('copy');
      ta.remove();
    }
    setCopied(true);
    window.setTimeout(() => setCopied(false), 2000);
  };
  return (
    <button type="button" className="btn btn-light btn-sm btn-icon" onClick={copy}>
      {copied ? <FiCheck className="text-success" /> : <FiCopy />} {copied ? 'Copied' : label}
    </button>
  );
}
