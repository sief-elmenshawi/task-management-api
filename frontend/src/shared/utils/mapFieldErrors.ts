export function mapFieldErrors(
  fieldErrors: Record<string, string[]>,
  fields: readonly string[],
): Record<string, string> {
  const result: Record<string, string> = {}

  for (const [key, messages] of Object.entries(fieldErrors)) {
    const message = messages[0]
    if (message === undefined) {
      continue
    }

    const field = fields.find((name) => name.toLowerCase() === key.toLowerCase())
    result[field ?? key] = message
  }

  return result
}
