
---
Bresenham:&nbsp; rational rise/run increments and cumulative error
---

Tension test PWM modulation period envelope will be straight lines: rise, hold, fall, wait:  
![](https://github.com/blekenbleu/Direct-Drive-harness-tension-tester/raw/main/test.png)  

Too many Bresenham line drawing "explanations" miss the essence:  
![](https://www.cs.helsinki.fi/group/goa/mallinnus/lines/bres1.gif)  
- For lines with rise <= run, every x increment will be populated and some y increments will be zero.
- There is no need to consider fractions.
- Only possible y increments are 0 or run.
- Given current error, the only question is whether error + rise is smaller than run - (error + rise).
	- for the above diagram, unit squares are run x run and *m* == rise
	- half of errors will be negative.

```
// run is always positive

int error;
// y-direction errors for abs(rise) <= run
for (int t = start; t <= end; t++)
{
	if (abs(error + rise) < abs(run - (error + rise)))
		error += rise;		// y not incremented
	else
	{
		error = run - (error + rise);
		y++;
	}
}
```
Conventional Bresenham for slopes > 1 is doomed,  
since samples are calculated at only fixed time (1/60 sec) increments
```
// t-direction errors for abs(rise) > run:
//  abs(rise) > run
int start = min, stop = max, inc = 1;
if (0 > rise)
{
  start = max;
  stop = min;
  inc = -1;
}

for (int y = start; y <= stop; y += inc)
{
    if (abs(error + run) < abs(abs(rise) - (error + run))
        error += run;      // t not incremented
    else
    {
        error = abs(rise) - (error + run);
        t++;
    }
}

```

... instead:
```
while (y <= stop && abs(error + run) < abs(abs(rise) - (error + run))
{
	error += run;
	y += inc
}
error = abs(rise) - (error + run);
```
Better still: refactor for y errors when abs(slope) > 1.
