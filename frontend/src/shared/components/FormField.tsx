import { cloneElement, isValidElement, type ReactElement, type ReactNode } from 'react'
import './FormField.css'

type ControlProps = {
  id?: string
  'aria-invalid'?: boolean
  'aria-describedby'?: string
}

export type FormFieldProps = {
  id: string
  label: string
  hint?: string
  error?: string
  children: ReactNode
}

export function FormField({ id, label, hint, error, children }: FormFieldProps) {
  const hintId = hint === undefined ? undefined : `${id}-hint`
  const errorId = error === undefined ? undefined : `${id}-error`
  const describedBy = [hintId, errorId].filter(Boolean).join(' ') || undefined

  const control = isValidElement(children)
    ? cloneElement(children as ReactElement<ControlProps>, {
        id,
        'aria-invalid': error === undefined ? undefined : true,
        'aria-describedby': describedBy,
      })
    : children

  return (
    <div className="field">
      <label className="field__label" htmlFor={id}>
        {label}
      </label>
      {control}
      {hint && (
        <p className="field__hint" id={hintId}>
          {hint}
        </p>
      )}
      {error && (
        <p className="field__error" id={errorId}>
          {error}
        </p>
      )}
    </div>
  )
}
