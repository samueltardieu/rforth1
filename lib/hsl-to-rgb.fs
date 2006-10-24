\ All units are comprised between 0 and 252 (it is the greatest value
\ divisible by 6 fitting on one byte). The algorithm used is the one
\ found on http://130.113.54.154/~monger/hsl-rgb.html

cvariable t1
cvariable t2

cvariable th
cvariable ts
cvariable tl

: make-t2 ( -- )
  tl c@ 126 < if tl c@ ts c@ 1+ * else tl @ ts @ + tl @ ts @ * - then t2 c!
;

: make-t1 ( -- ) tl c@ 2* t2 c@ - t1 c! ;

: to-t3-r ( -- t3 ) th c@ 84 + $ff and >w ; outw
: to-t3-g ( -- t3 ) th c@ >w ; outw
: to-t3-b ( -- t3 ) th c@ 84 - $ff and >w ; outw

: t3-to-color ( t3 -- color )
  w>
  dup 42 < if 6 * t2 c@ t1 c@ - * t1 c@ + >w exit then
  dup 126 < if drop t2 c@ >w exit then
  dup 168 < if 168 - 6 * t1 c@ t2 c@ - * t1 c@ + >w exit then
  drop t1 c@ >w
; inw outw

: to-r ( -- ) to-t3-r t3-to-color ;
: to-g ( -- ) to-t3-g t3-to-color ;
: to-b ( -- ) to-t3-b t3-to-color ;

: hsl-to-rgb ( h s l -- r g b )
  tl c! ts c! th c! make-t2 make-t1 to-r to-g to-b  ;
