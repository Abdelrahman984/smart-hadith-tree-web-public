import { test, expect } from '@playwright/test';
import { groupSourcesByCompanion } from '../src/features/isnad-tree/utils/groupSources';
import type { ComparativeIsnadNodeDto, IlalTariqDto } from '../src/types/api';

const src = (hadithId: string) => ({ hadithId, bookName: `book-${hadithId}`, hadithNumber: 1, matnSnippet: '' });
const node = (id: string, narratorId: string, stepOrder: number, ids: string[], name = narratorId): ComparativeIsnadNodeDto => ({
  id, narratorId, narratorName: name, knownAs: null, generationTier: null, stepOrder, parentNodeId: null, transmissionTerm: null,
  sourceHadithIds: ids, sourceBooks: [],
});

test.describe('groupSourcesByCompanion', () => {
  test('narrations through the same Companion are routes; another Companion makes a witness', () => {
    const nodes = [
      node('a0', 'C1', 0, ['h1']), node('a1', 'X', 1, ['h1']), node('a2', 'ABU-HURAYRA', 2, ['h1'], 'أبو هريرة'),
      node('b0', 'C2', 0, ['h2']), node('b1', 'Y', 1, ['h2']), node('b2', 'ABU-HURAYRA', 2, ['h2'], 'أبو هريرة'),
      node('c0', 'C3', 0, ['h3']), node('c1', 'Z', 1, ['h3']), node('c2', 'ALI', 2, ['h3'], 'علي'),
    ];
    const g = groupSourcesByCompanion({ sources: [src('h1'), src('h2'), src('h3')], nodes });
    expect(g.main).toEqual({ narratorId: 'ABU-HURAYRA', name: 'أبو هريرة' });
    expect(g.turuq.map((s) => s.hadithId)).toEqual(['h1', 'h2']);
    expect(g.shawahid.map((w) => [w.source.hadithId, w.companion.name])).toEqual([['h3', 'علي']]);
  });

  test('a tie goes to the first narration\'s Companion', () => {
    const nodes = [node('a', 'C', 0, ['h1']), node('a1', 'P', 1, ['h1']), node('b', 'D', 0, ['h2']), node('b1', 'Q', 1, ['h2'])];
    const g = groupSourcesByCompanion({ sources: [src('h1'), src('h2')], nodes });
    expect(g.main?.narratorId).toBe('P');
    expect(g.shawahid.map((w) => w.source.hadithId)).toEqual(['h2']);
  });

  test('a narration whose chain is unknown is treated as a route, never a witness', () => {
    const nodes = [node('a0', 'C', 0, ['h1']), node('a1', 'P', 1, ['h1'])];
    const g = groupSourcesByCompanion({ sources: [src('h1'), src('ghost')], nodes });
    expect(g.turuq.map((s) => s.hadithId)).toEqual(['h1', 'ghost']);
    expect(g.shawahid).toEqual([]);
  });

  test('several chains of one narration: the Companion most of them end at', () => {
    const nodes = [node('a1', 'P', 1, ['h1']), node('a2', 'P', 1, ['h1']), node('a3', 'Q', 1, ['h1'])];
    expect(groupSourcesByCompanion({ sources: [src('h1')], nodes }).main?.narratorId).toBe('P');
  });

  test('no sources, no groups', () => {
    expect(groupSourcesByCompanion({ sources: [], nodes: [] })).toEqual({ main: null, turuq: [], shawahid: [] });
  });

  const tariq = (hadithId: string, companionId: string | null, isShahid: boolean, companionName = companionId ?? ''): IlalTariqDto => ({
    hadithId, bookName: '', hadithNumber: 1, isMarfu: true, companionId, companionName, isShahid,
  });

  test('the split the server made wins: a witness is the one it flags, and a chain that stops short stays a route', () => {
    const g = groupSourcesByCompanion({
      sources: [src('h1'), src('h2'), src('h3'), src('h4')],
      nodes: [],
      ilalReport: {
        analyzedHadithIds: [], madars: [], findings: [], hasQadihah: false, summaryAr: '',
        turuq: [tariq('h1', 'ABU-MALIK', false, 'أبو مالك'), tariq('h2', null, false), tariq('h3', 'ALI', true, 'علي'), tariq('h4', 'ABU-MALIK', false, 'أبو مالك')],
      },
    });
    expect(g.main).toEqual({ narratorId: 'ABU-MALIK', name: 'أبو مالك' });
    expect(g.turuq.map((s) => s.hadithId)).toEqual(['h1', 'h2', 'h4']);
    expect(g.shawahid.map((w) => [w.source.hadithId, w.companion.name])).toEqual([['h3', 'علي']]);
  });

  test('a narration with several chains is a witness only when every chain is', () => {
    const g = groupSourcesByCompanion({
      sources: [src('h1'), src('h2')],
      nodes: [],
      ilalReport: {
        analyzedHadithIds: [], madars: [], findings: [], hasQadihah: false, summaryAr: '',
        turuq: [tariq('h1', 'P', false), tariq('h2', 'Q', true), tariq('h2', 'P', false)],
      },
    });
    expect(g.shawahid).toEqual([]);
    expect(g.turuq.map((s) => s.hadithId)).toEqual(['h1', 'h2']);
  });
});
