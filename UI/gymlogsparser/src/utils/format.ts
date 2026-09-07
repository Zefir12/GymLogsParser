export function formatDuration(seconds: number | null | undefined) {
  if (seconds === null || seconds === undefined) {
    return "";
  }

  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = seconds % 60;

  if (remainingSeconds === 0) {
    return `${minutes} min`;
  }

  return `${minutes}m ${remainingSeconds}s`;
}
