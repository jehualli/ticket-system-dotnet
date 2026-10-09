import { errorMessage, label } from './api';

describe('errorMessage', () => {
  it('uses the API message', () => {
    expect(errorMessage({ status: 403, error: { message: 'Only ADMIN can delete tickets' } }))
      .toBe('Only ADMIN can delete tickets');
  });

  it('lists validation errors per field', () => {
    const err = { status: 400, error: { message: 'Validation failed', fieldErrors: { title: ['required'], category: ['required'] } } };

    expect(errorMessage(err)).toBe('title: required; category: required');
  });

  it('falls back when the server sent no body', () => {
    expect(errorMessage({ status: 0, error: null })).toBe('Request failed (0)');
  });
});

describe('label', () => {
  it('makes enum values readable', () => {
    expect(label('IN_PROGRESS')).toBe('in progress');
  });
});
