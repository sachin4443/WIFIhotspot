import { BarElement, CategoryScale, Chart as ChartJS, Filler, Legend, LinearScale, LineElement, PointElement, Tooltip, type ChartOptions } from 'chart.js';
import { Bar, Line } from 'react-chartjs-2';

ChartJS.register(CategoryScale, LinearScale, BarElement, LineElement, PointElement, Tooltip, Legend, Filler);

/**
 * Validated light categorical palette (dataviz reference instance), assigned in fixed order.
 * Single-series charts use the brand indigo.
 */
export const SERIES = ['#2a78d6', '#eb6834', '#1baf7a', '#eda100', '#e87ba4', '#008300', '#6250d6', '#e34948'];
export const SINGLE = '#4f46e5';
const GRID = '#eef0f4';
const TICK = '#6b7280';

ChartJS.defaults.font.family = "'Inter Variable', system-ui, sans-serif";
ChartJS.defaults.font.size = 12;
ChartJS.defaults.color = TICK;

const tooltip = {
  backgroundColor: '#111827',
  padding: 10,
  cornerRadius: 8,
  titleFont: { weight: 600 as const },
  displayColors: true,
  boxPadding: 4,
};

export interface Series {
  label: string;
  data: number[];
}

interface BarProps {
  labels: string[];
  series: Series[];
  horizontal?: boolean;
  valueFormatter?: (v: number) => string;
  small?: boolean;
  ariaLabel: string;
}

export function BarChart({ labels, series, horizontal, valueFormatter, small, ariaLabel }: BarProps) {
  const multi = series.length > 1;
  const options: ChartOptions<'bar'> = {
    responsive: true,
    maintainAspectRatio: false,
    animation: false,
    indexAxis: horizontal ? 'y' : 'x',
    interaction: { mode: 'index', intersect: false },
    plugins: {
      legend: { display: multi, position: 'top', align: 'end', labels: { boxWidth: 10, boxHeight: 10, useBorderRadius: true, borderRadius: 2 } },
      tooltip: { ...tooltip, callbacks: valueFormatter ? { label: (c) => `${c.dataset.label}: ${valueFormatter(Number(c.raw))}` } : undefined },
    },
    scales: {
      x: { grid: { display: horizontal, color: GRID }, border: { display: false }, ticks: horizontal ? { precision: 0, callback: valueFormatter ? (v) => valueFormatter(Number(v)) : undefined } : { autoSkip: true, maxRotation: 0 } },
      y: { grid: { display: !horizontal, color: GRID }, border: { display: false }, beginAtZero: true, ticks: horizontal ? { autoSkip: false } : { precision: 0, callback: valueFormatter ? (v) => valueFormatter(Number(v)) : undefined } },
    },
  };
  const data = {
    labels,
    datasets: series.map((s, i) => ({
      label: s.label,
      data: s.data,
      backgroundColor: multi ? SERIES[i % SERIES.length] : SINGLE,
      borderRadius: 4,
      borderSkipped: 'start' as const,
      maxBarThickness: horizontal ? 18 : 32,
      categoryPercentage: 0.7,
      barPercentage: 0.9,
    })),
  };
  return (
    <div className={`nxt-chart ${small ? 'is-sm' : ''}`} role="img" aria-label={ariaLabel}>
      <Bar options={options} data={data} />
    </div>
  );
}

export function LineChart({ labels, series, valueFormatter, small, ariaLabel }: Omit<BarProps, 'horizontal'>) {
  const multi = series.length > 1;
  const options: ChartOptions<'line'> = {
    responsive: true,
    maintainAspectRatio: false,
    animation: false,
    interaction: { mode: 'index', intersect: false },
    plugins: {
      legend: { display: multi, position: 'top', align: 'end', labels: { boxWidth: 10, boxHeight: 10, useBorderRadius: true, borderRadius: 2 } },
      tooltip: { ...tooltip, callbacks: valueFormatter ? { label: (c) => `${c.dataset.label}: ${valueFormatter(Number(c.raw))}` } : undefined },
    },
    scales: {
      x: { grid: { display: false }, border: { display: false }, ticks: { autoSkip: true, maxRotation: 0 } },
      y: { grid: { color: GRID }, border: { display: false }, beginAtZero: true, ticks: { precision: 0, callback: valueFormatter ? (v) => valueFormatter(Number(v)) : undefined } },
    },
  };
  const data = {
    labels,
    datasets: series.map((s, i) => {
      const color = multi ? SERIES[i % SERIES.length] : SINGLE;
      return {
        label: s.label, data: s.data, borderColor: color, backgroundColor: multi ? color : 'rgba(79,70,229,0.08)',
        fill: !multi, borderWidth: 2, pointRadius: 0, pointHoverRadius: 5, pointHitRadius: 12, tension: 0.25,
      };
    }),
  };
  return (
    <div className={`nxt-chart ${small ? 'is-sm' : ''}`} role="img" aria-label={ariaLabel}>
      <Line options={options} data={data} />
    </div>
  );
}
