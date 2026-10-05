/** Route params arrive percent-encoded for Arabic names; a malformed value is used as it is. */
export function decodeParam(value: string): string {
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}

export function bookHref(bookName: string, chapter?: string): string {
  const base = `/books/${encodeURIComponent(bookName)}`;
  return chapter ? `${base}/chapters/${encodeURIComponent(chapter)}` : base;
}
