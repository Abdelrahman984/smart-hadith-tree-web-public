/** True when the API answering on the default port is the test fixture (`mock-api.mjs`), not a real backend. */
export async function mockApiAvailable(): Promise<boolean> {
  try {
    const res = await fetch('http://localhost:5147/api/__health');
    return res.ok && (await res.json()).mock === true;
  } catch {
    return false;
  }
}

export const MOCK_API_SKIP_REASON =
  'Needs the fixture API on :5147. Stop the real API (or run these tests where nothing listens on 5147).';
