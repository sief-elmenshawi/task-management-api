import './Spinner.css'

type SpinnerProps = {
  label?: string
}

export function Spinner({ label = 'Loading…' }: SpinnerProps) {
  return <span className="spinner" role="status" aria-label={label} />
}
