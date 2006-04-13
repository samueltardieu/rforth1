class Snooper:

    def __init__ (self, fd):
        self.fd = fd

    def write (self, data):
        print ">>> %s" % data
        self.fd.write (data)
        self.fd.flush ()

    def read (self, l = None):
        if l is None:
	  data = self.fd.read ()
	else:
	  data = self.fd.read (l)
        print "<<< %s" % data
        return data

    def close (self):
        self.fd.close ()

    def flush (self):
        self.fd.flush ()
        
