import { describe, it, expect } from 'vitest';
import { toUCI, fromUCI } from '$lib/uciEngine';
import { GameConfig } from '$lib/config';

// The engine's UCI wire format is double-letter: letter(y) + letter(x),
// matching backend uci.MoveToString / uci.ParseMove.
describe('UCI Coordinate Conversion', () => {
  describe('toUCI', () => {
    it('should convert origin (0,0) to aa', () => {
      expect(toUCI(0, 0)).toBe('aa');
    });

    it('should convert max column (14,0) to ao', () => {
      expect(toUCI(14, 0)).toBe('ao');
    });

    it('should convert max row (0,14) to oa', () => {
      expect(toUCI(0, 14)).toBe('oa');
    });

    it('should convert corner (14,14) to oo', () => {
      expect(toUCI(14, 14)).toBe('oo');
    });

    it('should convert center (7,7) to hh', () => {
      expect(toUCI(7, 7)).toBe('hh');
    });

    it('should encode row first, column second', () => {
      expect(toUCI(2, 5)).toBe('fc');
    });

    it('should throw for x out of bounds', () => {
      expect(() => toUCI(-1, 0)).toThrow();
      expect(() => toUCI(GameConfig.boardSize, 0)).toThrow();
    });

    it('should throw for y out of bounds', () => {
      expect(() => toUCI(0, -1)).toThrow();
      expect(() => toUCI(0, GameConfig.boardSize)).toThrow();
    });
  });

  describe('fromUCI', () => {
    it('should convert aa to (0,0)', () => {
      expect(fromUCI('aa')).toEqual({ x: 0, y: 0 });
    });

    it('should convert ao to (14,0)', () => {
      expect(fromUCI('ao')).toEqual({ x: 14, y: 0 });
    });

    it('should convert oa to (0,14)', () => {
      expect(fromUCI('oa')).toEqual({ x: 0, y: 14 });
    });

    it('should convert hh to (7,7)', () => {
      expect(fromUCI('hh')).toEqual({ x: 7, y: 7 });
    });

    it('should decode row first, column second', () => {
      expect(fromUCI('fc')).toEqual({ x: 2, y: 5 });
    });

    it('should be case-insensitive', () => {
      expect(fromUCI('AA')).toEqual({ x: 0, y: 0 });
      expect(fromUCI('HH')).toEqual({ x: 7, y: 7 });
    });

    it('should throw for out-of-range letters', () => {
      expect(() => fromUCI('q1')).toThrow();
      expect(() => fromUCI('aq')).toThrow();
      expect(() => fromUCI('za')).toThrow();
    });

    it('should throw for wrong-length strings and letter+digit inputs', () => {
      expect(() => fromUCI('')).toThrow();
      expect(() => fromUCI('a')).toThrow();
      expect(() => fromUCI('aaa')).toThrow();
      expect(() => fromUCI('h8')).toThrow();
    });
  });

  describe('round-trip', () => {
    it(`should round-trip all ${GameConfig.totalCells} board positions`, () => {
      for (let x = 0; x < GameConfig.boardSize; x++) {
        for (let y = 0; y < GameConfig.boardSize; y++) {
          const uci = toUCI(x, y);
          const result = fromUCI(uci);
          expect(result).toEqual({ x, y });
        }
      }
    });
  });
});
