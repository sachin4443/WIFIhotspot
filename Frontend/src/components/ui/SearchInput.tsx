import { useEffect, useState } from 'react';
import { FiSearch } from 'react-icons/fi';
import { useDebounce } from '../../hooks/useDebounce';

interface SearchInputProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
}

/** Search box that reports changes after the user pauses typing. */
export function SearchInput({ value, onChange, placeholder = 'Search…' }: SearchInputProps) {
  const [text, setText] = useState(value);
  const debounced = useDebounce(text, 350);
  useEffect(() => setText(value), [value]);
  useEffect(() => {
    if (debounced !== value) onChange(debounced);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debounced]);

  return (
    <div className="nxt-search">
      <FiSearch aria-hidden="true" />
      <input type="search" className="form-control" placeholder={placeholder} value={text} onChange={(e) => setText(e.target.value)} aria-label={placeholder} />
    </div>
  );
}
