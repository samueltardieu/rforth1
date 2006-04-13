\ Found on http://www.saunalahti.fi/elepal/pic/muldiv18.html

variable temp_w variable temp_l variable temp_e
cvariable temp_c0 cvariable temp_c1 cvariable temp_c2 cvariable temp_c3


code mulw
    temp_w 1 + ,w ,a movf
    temp_l 1 + ,a mulwf
    PRODH temp_e 1 + movff
    PRODL temp_e movff
    temp_w ,w ,a movf
    temp_l ,a mulwf
    PRODH temp_c3 movff
    PRODL temp_w movff
    temp_l 1 + ,a mulwf
    PRODL ,w ,a movf
    temp_c3 ,f ,a addwf
    PRODH ,w ,a movf
    temp_e ,f ,a addwfc
    WREG ,a clrf
    temp_e 1 + ,f ,a addwfc
    temp_w 1 + ,w ,a movf
    temp_l ,a mulwf
    PRODL ,w ,a movf
    temp_c3 ,w ,a addwf
    temp_w 1 + ,a movwf
    PRODH ,w ,a movf
    temp_e ,f ,a addwfc
    WREG ,a clrf
    temp_e 1 + ,f ,a addwfc
    return
;code

forward div323
forward div324
forward div325
forward div326

code div32
    temp_c0 ,a clrf
    temp_c1 ,a clrf
    temp_c2 ,a clrf
    -32 movlw
    temp_c3 ,a movwf
label div322
    C ,a bcf
    temp_w ,f ,a rlcf
    temp_w 1 + ,f ,a rlcf
    temp_e ,f ,a rlcf
    temp_e 1 + ,f ,a rlcf
    temp_c2 ,f ,a rlcf
    temp_c1 ,f ,a rlcf
    temp_c0 ,f ,a rlcf
    temp_l ,w ,a movf
    temp_c2 ,f ,a subwf
    temp_l 1 + ,w ,a movf
    temp_c1 ,w ,a subwfb
    div324 bc
    temp_c0 ,a tstfsz
    div323 bra
    temp_l ,w ,a movf
    temp_c2 ,f ,a addwf
    div325 bra
label div323
    temp_c0 ,f ,a decf
label div324
    temp_c1 ,a movwf
    temp_w 0 ,a bsf
    temp_c3 4 ,a btfss
    div326 bra
label div325
    temp_c3 ,f ,a incfsz
    div322 bra
    temp_c2 temp_e movff
    temp_c1 temp_e 1 + movff   
    return
label div326
    0xff movlw
    temp_w 1 + ,a movwf
    temp_w ,a movwf
    temp_e 1 + ,a movwf
    temp_e ,a movwf
    return
;code

cvariable math-flags
math-flags 0 bit negative

: normalize-tos dup 0< if negative bit-toggle negate then ;
: normalize-tos-2 normalize-tos >r normalize-tos r> ;
: normalize-tos-3 normalize-tos >r normalize-tos-2 r> ;
: apply-sign negative bit-set? if negate then ;
: negate32 >r negate dup 0= if r> negate else r> invert then ;
: apply-sign32 negative bit-set? if negate32 then ;

: */ ( n1 n2 n3 -- n1*n2/n3 )
  negative bit-clr normalize-tos-3
  intr-protect
  >r temp_w ! temp_l ! mulw r> temp_l ! div32 temp_w @
  intr-unprotect
  apply-sign ;

: / ( n1 n2 -- n1/n2 )
  negative bit-clr normalize-tos-2
  intr-protect
  temp_l ! 0 temp_e ! temp_w ! div32 temp_w @
  intr-unprotect
  apply-sign ;

: /32 ( n1l n1h n2 -- n1/n2 )
  negative bit-clr normalize-tos-2
  intr-protect
  temp_l ! temp_e ! temp_w ! div32 temp_w @
  intr-unprotect
  apply-sign ;

: *32 ( n1 n2 -- n1*n2l n1*n2h )
  negative bit-clr normalize-tos-2
  intr-protect
  temp_w ! temp_l ! mulw temp_w @ temp_e @
  intr-unprotect
  apply-sign32 ;

  : +32 ( n1l n1h n2l n2h -- [n1+n2]l [n1+n2]h )
    >r swap >r + C bit-set? if 1 else 0 then r> + r> + ;
