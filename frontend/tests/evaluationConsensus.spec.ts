import { test, expect } from '@playwright/test';
import { analyzeEvaluations, campOf } from '../src/features/narrator-details/utils/evaluationConsensus';

const ev = (verdictRating: string | null, scholarName = 's') => ({
  scholarName,
  evaluationText: 'x',
  sourceBook: null,
  verdictRating,
});

test.describe('analyzeEvaluations', () => {
  test('flags a dispute when ta\'dil and jarh are both recorded', () => {
    const r = analyzeEvaluations([ev('reliable'), ev('weak'), ev('mostly_reliable')]);
    expect(r.status).toBe('dispute');
    expect(r.tadilCount).toBe(2);
    expect(r.jarhCount).toBe(1);
    expect(r.groups.map((g) => g.camp)).toEqual(['tadil', 'jarh']);
  });

  test('treats "unknown" (majhul) as against ta\'dil', () => {
    expect(analyzeEvaluations([ev('reliable'), ev('unknown')]).status).toBe('dispute');
  });

  test('reports agreement only with two or more classified verdicts on one side', () => {
    const r = analyzeEvaluations([ev('reliable'), ev('mostly_reliable')]);
    expect(r.status).toBe('agree');
    expect(r.agreedCamp).toBe('tadil');
    expect(analyzeEvaluations([ev('weak'), ev('abandoned')]).agreedCamp).toBe('jarh');
  });

  test('a single classified verdict is neither agreement nor dispute', () => {
    expect(analyzeEvaluations([ev('reliable'), ev(null)]).status).toBe('single');
  });

  test('no classified verdict', () => {
    expect(analyzeEvaluations([]).status).toBe('none');
    expect(analyzeEvaluations([ev(null), ev('something-else')]).status).toBe('none');
  });

  test('keeps original indexes and puts unclassified last', () => {
    const r = analyzeEvaluations([ev(null), ev('weak'), ev('reliable')]);
    expect(r.groups.map((g) => g.camp)).toEqual(['tadil', 'jarh', 'unclassified']);
    expect(r.groups[0].items[0].index).toBe(2);
    expect(r.groups[2].items[0].index).toBe(0);
  });

  test('verdict keys are case-insensitive', () => {
    expect(campOf('Reliable')).toBe('tadil');
    expect(campOf(undefined)).toBe('unclassified');
  });
});

import { narratorEvidence } from '../src/features/narrator-details/utils/evaluationConsensus';

test.describe('narratorEvidence', () => {
  test('no evaluations: insufficient, never a verdict', () => {
    expect(narratorEvidence(analyzeEvaluations([]), 0).status).toBe('insufficient');
  });
  test('agreeing attributed evaluations: supported', () => {
    expect(narratorEvidence(analyzeEvaluations([ev('reliable'), ev('reliable')]), 2).status).toBe('supported');
  });
  test('dispute, a single verdict, or unclassified text: needs verification', () => {
    expect(narratorEvidence(analyzeEvaluations([ev('reliable'), ev('weak')]), 2).status).toBe('verify');
    expect(narratorEvidence(analyzeEvaluations([ev('reliable')]), 1).status).toBe('verify');
    expect(narratorEvidence(analyzeEvaluations([ev(null)]), 1).status).toBe('verify');
  });
});
