import { TimeDurationPipe } from './time-duration.pipe';

describe('TimeDurationPipe', () => {
  it('create an instance', () => {
    const pipe = new TimeDurationPipe();
    expect(pipe).toBeTruthy();
  });
});
