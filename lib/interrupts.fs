cvariable high-wtemp
cvariable high-fsr0ltemp
cvariable high-fsr0htemp

create secstack-high 22 allot
create secrstack-high 6 allot

: save-everything-high ( -- )
  w> high-wtemp c!
  FSR0L c@ high-fsr0ltemp c!
  FSR0H c@ high-fsr0htemp c!
  secstack-high FSR0L !
  STATUS c@
  FSR1L @
  FSR2L @
  secrstack-high FSR2L !
; inline

: restore-everything-high ( -- )
  FSR2L !
  FSR1L !
  STATUS c!
  high-fsr0htemp c@ FSR0H c!
  high-fsr0ltemp c@ FSR0L c!
  high-wtemp c@ >w
; inline

cvariable low-wtemp
cvariable low-fsr0ltemp
cvariable low-fsr0htemp

create secstack-low 22 allot
create secrstack-low 6 allot

: save-everything-low ( -- )
  w> low-wtemp c!
  FSR0L c@ low-fsr0ltemp c!
  FSR0H c@ low-fsr0htemp c!
  secstack-low FSR0L !
  STATUS c@
  FSR1L @
  FSR2L @
  secrstack-low FSR2L !
; inline

: restore-everything-low ( -- )
  FSR2L !
  FSR1L !
  STATUS c!
  low-fsr0htemp c@ FSR0H c!
  low-fsr0ltemp c@ FSR0L c!
  low-wtemp c@ >w
; inline
