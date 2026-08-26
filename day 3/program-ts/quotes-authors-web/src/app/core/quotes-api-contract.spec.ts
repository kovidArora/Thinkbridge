// Characterization test: pins the REAL, currently-running QuotesApi's wire contract.
// No mocking, no HttpClient, no interceptors — just fetch() against the live API.
// Requires QuotesApi to be running on http://localhost:5067 (dotnet run, Development env).
// This must be green BEFORE any HttpClient/interceptor code is written against this contract.

const API_BASE = 'http://localhost:5067';

describe('QuotesApi contract: GET /api/quotes', () => {
  it('a valid page/size request returns 200 with {id, author, text} items', async () => {
    const response = await fetch(`${API_BASE}/api/quotes?page=1&size=5`);
    expect(response.status).toBe(200);

    const body = await response.json();
    expect(Array.isArray(body)).toBe(true);
    expect(body.length).toBeGreaterThan(0);

    for (const item of body) {
      expect(typeof item.id).toBe('number');
      expect(typeof item.author).toBe('string');
      expect(typeof item.text).toBe('string');
    }
  });

  it('page=0 returns 400 shaped like ValidationProblemDetails', async () => {
    const response = await fetch(`${API_BASE}/api/quotes?page=0&size=5`);
    expect(response.status).toBe(400);

    const body = await response.json();
    expect(typeof body.title).toBe('string');
    expect(body.status).toBe(400);
    expect(body.errors).toBeTruthy();
    expect(Array.isArray(body.errors.page)).toBe(true);
  });

  it('size=0 returns 400 shaped like ValidationProblemDetails', async () => {
    const response = await fetch(`${API_BASE}/api/quotes?page=1&size=0`);
    expect(response.status).toBe(400);

    const body = await response.json();
    expect(body.status).toBe(400);
    expect(Array.isArray(body.errors.size)).toBe(true);
  });
});

describe('QuotesApi contract: POST /api/quotes 400 shape (NOT ProblemDetails)', () => {
  it('an invalid create request returns 400 as a bare JSON string, not ValidationProblemDetails', async () => {
    const login = await fetch(`${API_BASE}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'test@example.com', password: 'Password123!' }),
    });
    const { access_token } = await login.json();

    const response = await fetch(`${API_BASE}/api/quotes`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${access_token}`,
      },
      body: JSON.stringify({ author: '', text: '' }),
    });
    expect(response.status).toBe(400);

    const body = await response.json();
    // This is the real, currently-verified shape: a bare string, NOT { title, status, errors }.
    expect(typeof body).toBe('string');
    expect(body.length).toBeGreaterThan(0);
  });
});
