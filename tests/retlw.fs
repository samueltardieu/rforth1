: hex-to-int 
	switchw
		 48 casew 0 >w
		 49 casew 1 >w
		 50 casew 2 >w
		 51 casew 3 >w
		 52 casew 4 >w
		 53 casew 5 >w
		 54 casew 6 >w
		 55 casew 7 >w
		 56 casew 8 >w
		 57 casew 9 >w
		 65 casew 10 >w
		 66 casew 11 >w
		 67 casew 12 >w
		 68 casew 13 >w
		 69 casew 14 >w
		 32 casew 255 >w
		 defaultw 0 >w
	endswitchw
; inw outw

: main ( -- ) 17 hex-to-int drop ;
