import { FiChevronLeft, FiChevronRight } from 'react-icons/fi';

interface PaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}

export function Pagination({ page, pageSize, totalCount, onPageChange }: PaginationProps) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  if (totalCount === 0) return null;
  const from = (page - 1) * pageSize + 1;
  const to = Math.min(totalCount, page * pageSize);
  return (
    <div className="nxt-pagination">
      <span className="font-tabular">
        {from.toLocaleString()}–{to.toLocaleString()} of {totalCount.toLocaleString()}
      </span>
      <div className="d-flex align-items-center gap-2">
        <button type="button" className="btn btn-light btn-sm btn-icon" disabled={page <= 1} onClick={() => onPageChange(page - 1)} aria-label="Previous page">
          <FiChevronLeft aria-hidden="true" /> <span className="d-none d-sm-inline">Prev</span>
        </button>
        <span className="font-tabular">Page {page} / {totalPages}</span>
        <button type="button" className="btn btn-light btn-sm btn-icon" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)} aria-label="Next page">
          <span className="d-none d-sm-inline">Next</span> <FiChevronRight aria-hidden="true" />
        </button>
      </div>
    </div>
  );
}
